using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Do_Not_Disturb
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static void Room_Notify_RoomShapeChanged_Postfix(Room __instance)
        {
#if DEBUG
            Log.Message($"Do Not Disturb :: Room shape changed → invalidating choke-point cache for {__instance.Role.label} #{__instance.ID}");
#endif
            ChokePointDetector.InvalidateCache(__instance);
        }

        private static void Pawn_DraftController_Drafted_Postfix(Pawn_DraftController __instance)
        {
            Pawn pawn = __instance.pawn;

            if (pawn.Drafted)
            {
                Room room = pawn.GetRoom();
                if (room != null)
                {
                    DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
                }

#if DEBUG
                Log.Message($"Do Not Disturb :: Unlocked doors for drafted pawn {pawn.Name}");
#endif
            }
        }

        private static void Building_Door_GetGizmos_Postfix(Building_Door __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.Faction != Faction.OfPlayer)
            {
                return;
            }

            DoNotDisturbManager manager = __instance.Map?.GetComponent<DoNotDisturbManager>();
            if (manager == null)
            {
                return;
            }

            Command_Toggle dndToggle = new Command_Toggle
            {
                defaultLabel = "DND_DoorToggle".Translate(),
                defaultDesc = "DND_DoorToggleDesc".Translate(),
                icon = TexCommand.HoldOpen,
                isActive = () => manager.IsDndEnabled(__instance),
                toggleAction = () => manager.SetDndEnabled(__instance, !manager.IsDndEnabled(__instance))
            };

            List<Gizmo> gizmoList = new List<Gizmo>(__result);
            gizmoList.Add(dndToggle);
            __result = gizmoList;
        }

        private static void Pawn_JobTracker_EndCurrentJob_Postfix(Pawn_JobTracker __instance)
        {
            Pawn pawn = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>(__instance, "pawn");
            if (pawn == null)
            {
                Log.Message($"[DND] EndCurrentJob postfix: pawn is null");
                return;
            }

            DoNotDisturbManager manager = pawn.Map?.GetComponent<DoNotDisturbManager>();
            if (manager == null)
            {
                Log.Message($"[DND] EndCurrentJob postfix: manager is null");
                return;
            }

            if (!manager.IsPawnDndActive(pawn))
            {
                Log.Message($"[DND] EndCurrentJob postfix for {pawn.LabelShort}: pawn is not DND-active, skipping unlock");
                return;
            }

            Room room = pawn.GetRoom();
            Log.Message($"[DND] EndCurrentJob postfix for DND-active pawn {pawn.LabelShort}, room={room?.Role.label ?? "NULL"}");
            
            if (room != null)
            {
                Log.Message($"[DND] Unlocking doors for {pawn.LabelShort} (job ended, was DND-active)");
                DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
                manager.PawnEndedDnd(pawn);
                Log.Message($"[DND] Doors unlocked on job end");
            }
        }

        private static void JobDriver_LayDown_MakeNewToils_Postfix(JobDriver_LayDown __instance, ref IEnumerable<Toil> __result)
        {
            Log.Message($"[DND] JobDriver_LayDown.MakeNewToils postfix called for {__instance.pawn.LabelShort}");
            
            List<Toil> toils = new List<Toil>(__result);
            Log.Message($"[DND] LayDown has {toils.Count} toils before modification");
            
            if (toils.Count >= 2)
            {
                Log.Message($"[DND] Inserting LockRoomDoors toil at index 2");
                toils.Insert(2, Toils_DoNotDisturb.LockRoomDoors());
            }
            else
            {
                Log.Warning($"[DND] LayDown toils count < 2, cannot insert lock toil at index 2");
            }
            
            Log.Message($"[DND] Adding UnlockRoomDoors toil at end");
            toils.Add(Toils_DoNotDisturb.UnlockRoomDoors());
            Log.Message($"[DND] LayDown now has {toils.Count} toils after modification");
            
            __result = toils;
        }

        private static void JobDriver_Lovin_MakeNewToils_Postfix(JobDriver_Lovin __instance, ref IEnumerable<Toil> __result)
        {
            List<Toil> toils = new List<Toil>(__result);
            if (toils.Count >= 2)
            {
                toils.Insert(2, Toils_DoNotDisturb.LockRoomDoors());
            }
            toils.Add(Toils_DoNotDisturb.UnlockRoomDoors());
            __result = toils;
        }

        private static void JobDriver_RelaxAlone_MakeNewToils_Postfix(JobDriver_RelaxAlone __instance, ref IEnumerable<Toil> __result)
        {
            List<Toil> toils = new List<Toil>(__result);
            if (toils.Count >= 1)
            {
                toils.Insert(1, Toils_DoNotDisturb.LockRoomDoors());
            }
            toils.Add(Toils_DoNotDisturb.UnlockRoomDoors());
            __result = toils;
        }

        private static void JobDriver_TendPatient_MakeNewToils_Postfix(JobDriver __instance, ref IEnumerable<Toil> __result)
        {
            Pawn patient = __instance.job?.GetTarget(TargetIndex.A).Thing as Pawn;
            if (patient != null)
            {
                List<Toil> toils = new List<Toil>(__result);
                toils.Insert(0, Toils_DoNotDisturb.UnlockDoorsForMedicalTreatment(patient));
                __result = toils;
            }
        }

        static HarmonyPatches()
        {
            try
            {
                Harmony harmony = new Harmony("dingo.donotdisturb");

#if DEBUG
                Harmony.DEBUG = true;
#endif

                MethodInfo pawnDraftSetter = AccessTools.PropertySetter(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted));
                harmony.Patch(pawnDraftSetter,
                    prefix: null,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Pawn_DraftController_Drafted_Postfix)));

                MethodInfo roomShapeChanged = AccessTools.Method(typeof(Room), nameof(Room.Notify_RoomShapeChanged));
                harmony.Patch(roomShapeChanged,
                    prefix: null,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Room_Notify_RoomShapeChanged_Postfix)));

                MethodInfo doorGetGizmos = AccessTools.Method(typeof(Building_Door), nameof(Building_Door.GetGizmos));
                harmony.Patch(doorGetGizmos,
                    prefix: null,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Building_Door_GetGizmos_Postfix)));

                MethodInfo endCurrentJob = AccessTools.Method(typeof(Pawn_JobTracker), "EndCurrentJob");
                if (endCurrentJob != null)
                {
                    Log.Message("[DND] Patching Pawn_JobTracker.EndCurrentJob");
                    harmony.Patch(endCurrentJob,
                        prefix: null,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Pawn_JobTracker_EndCurrentJob_Postfix)));
                }
                else
                {
                    Log.Warning("[DND] Could not find Pawn_JobTracker.EndCurrentJob method");
                }

                MethodInfo layDownMakeNewToils = AccessTools.Method(typeof(JobDriver_LayDown), "MakeNewToils");
                if (layDownMakeNewToils != null)
                {
                    harmony.Patch(layDownMakeNewToils,
                        prefix: null,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.JobDriver_LayDown_MakeNewToils_Postfix)));
                }

                MethodInfo lovinMakeNewToils = AccessTools.Method(typeof(JobDriver_Lovin), "MakeNewToils");
                if (lovinMakeNewToils != null)
                {
                    harmony.Patch(lovinMakeNewToils,
                        prefix: null,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.JobDriver_Lovin_MakeNewToils_Postfix)));
                }

                MethodInfo relaxAloneMakeNewToils = AccessTools.Method(typeof(JobDriver_RelaxAlone), "MakeNewToils");
                if (relaxAloneMakeNewToils != null)
                {
                    harmony.Patch(relaxAloneMakeNewToils,
                        prefix: null,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.JobDriver_RelaxAlone_MakeNewToils_Postfix)));
                }

                Type jobDriverTendPatient = AccessTools.TypeByName("RimWorld.JobDriver_TendPatient");
                if (jobDriverTendPatient != null)
                {
                    MethodInfo tendPatientMakeNewToils = AccessTools.Method(jobDriverTendPatient, "MakeNewToils");
                    if (tendPatientMakeNewToils != null)
                    {
                        harmony.Patch(tendPatientMakeNewToils,
                            prefix: null,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.JobDriver_TendPatient_MakeNewToils_Postfix)));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Do Not Disturb :: Failed to apply Harmony patches: {ex}");
            }
        }
    }
}

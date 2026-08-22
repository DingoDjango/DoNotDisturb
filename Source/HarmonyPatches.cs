using System;
using System.Collections.Generic;
using System.Linq;
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
        internal static void DND_Log(string message, object details = null)
        {
            if (details != null)
            {
                message = $"{message}: {details}";
            }

            Log.Message("[DND] " + message);
        }
        
        private static void Pawn_DraftController_Drafted_Postfix(Pawn_DraftController __instance)
        {
            if (__instance.Drafted && __instance.pawn?.GetRoom() is Room room)
            {
                DoNotDisturbUtility.SetRoomDoors(room, forbid: false, __instance.pawn.Map);
                HarmonyPatches.DND_Log($"Unlocked doors for drafted pawn", new { Pawn = __instance.pawn.Name });
            }
        }

        private static void Room_Notify_RoomShapeChanged_Postfix(Room __instance)
        {
            HarmonyPatches.DND_Log($"Room shape changed", new { Role = __instance.Role.label, RoomId = __instance.ID });
            ChokePointDetector.InvalidateCache(__instance);
        }

        private static void AddLockToil(ref IEnumerable<Toil> __result, int insertIndex)
        {
            List<Toil> toils = new List<Toil>(__result);
            if (toils.Count > insertIndex)
            {
                toils.Insert(insertIndex, Toils_DoNotDisturb.LockRoomDoors());
            }
            __result = toils;
        }

        private static void JobDriver_LayDown_MakeNewToils_Postfix(JobDriver_LayDown __instance, ref IEnumerable<Toil> __result)
        {
            AddLockToil(ref __result, 2);
        }

        private static void JobDriver_Lovin_MakeNewToils_Postfix(JobDriver_Lovin __instance, ref IEnumerable<Toil> __result)
        {
            AddLockToil(ref __result, 2);
        }

        private static void JobDriver_RelaxAlone_MakeNewToils_Postfix(JobDriver_RelaxAlone __instance, ref IEnumerable<Toil> __result)
        {
            AddLockToil(ref __result, 1);
        }

        private static void JobDriver_MakeNewToils_Postfix(JobDriver __instance)
        {
            Room room = __instance.pawn?.GetRoom();
            if (room == null)
            {
                return;
            }

            DoNotDisturbManager manager = __instance.pawn.Map?.GetComponent<DoNotDisturbManager>();
            manager?.SyncRoomDoorsForJob(__instance, room);
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
                return;
            }

            DoNotDisturbManager manager = pawn.Map?.GetComponent<DoNotDisturbManager>();
            if (manager == null)
            {
                return;
            }

            if (!manager.IsPawnDndActive(pawn))
            {
                return;
            }

#if DEBUG
            Log.Message($"[DND] EndCurrentJob postfix for DND-active pawn {pawn.LabelShort} (ended job: {pawn.CurJob?.def.defName ?? "NULL"})");
#endif
            
            // Unlock doors synchronously here, not deferred to map component
            Room room = manager.GetDndRoom(pawn);
            List<Building_Door> trackedDoors = manager.GetDndDoors(pawn).ToList();

            if (room != null || trackedDoors.Count > 0)
            {
                foreach (Building_Door door in trackedDoors)
                {
                    if (door != null && door.Spawned)
                    {
                        CompForbiddable comp = door.GetComp<CompForbiddable>();
                        if (comp != null)
                        {
                            bool originalState = manager.GetOriginalDoorState(pawn, door);
                            comp.Forbidden = originalState;
#if DEBUG
                            Log.Message($"[DND] Restored door {door.Label} to original state: forbidden={originalState} (pawn: {pawn.LabelShort})");
#endif
                        }
                    }
                }

                if (room != null)
                {
                    DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
                }

                manager.PawnEndedDnd(pawn);
            }
        }

        private static void CompForbiddable_Forbidden_Postfix(CompForbiddable __instance)
        {
            Building_Door door = __instance.parent as Building_Door;
            if (door == null || door.Faction != Faction.OfPlayer)
            {
                return;
            }

            DoNotDisturbManager manager = door.Map?.GetComponent<DoNotDisturbManager>();
            if (manager == null)
            {
                return;
            }

            manager.ClearDoorFromAllPawns(door);
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

                MethodInfo forbiddableSetter = AccessTools.PropertySetter(typeof(CompForbiddable), nameof(CompForbiddable.Forbidden));
                if (forbiddableSetter != null)
                {
                    harmony.Patch(forbiddableSetter,
                        prefix: null,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.CompForbiddable_Forbidden_Postfix)));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Do Not Disturb :: Failed to apply Harmony patches: {ex}");
            }
        }
    }
}

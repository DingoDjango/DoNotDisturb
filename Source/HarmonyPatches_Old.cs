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

        private static void JobDriver_NotifyStarting_Postfix(JobDriver __instance)
        {
            Pawn pawn = __instance.pawn;
            Room room = pawn?.GetRoom();
            
            if (room == null || __instance.job == null)
            {
                return;
            }

            if (__instance is JobDriver_LayDown || __instance is JobDriver_Lovin || __instance is JobDriver_RelaxAlone)
            {
                if (DoNotDisturbUtility.ShouldLockRoom(room, pawn))
                {
                    DoNotDisturbUtility.SetRoomDoors(room, forbid: true, pawn.Map);
#if DEBUG
                    Log.Message($"Do Not Disturb :: Job {__instance.GetType().Name} started, locked doors");
#endif
                }
            }
            else if (__instance.job.def == JobDefOf.TendPatient)
            {
                Pawn patient = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
                if (patient != null)
                {
                    Room patientRoom = patient.GetRoom();
                    if (patientRoom != null && DoNotDisturbUtility.ShouldUnlockForMedical(patientRoom, patient))
                    {
                        DoNotDisturbUtility.SetRoomDoors(patientRoom, forbid: false, patient.Map);
#if DEBUG
                        Log.Message($"Do Not Disturb :: TendPatient job started, unlocked doors for {patient.LabelShort}");
#endif
                    }
                }
            }
        }

        private static void Pawn_JobTracker_EndCurrentJob_Postfix(Pawn_JobTracker __instance)
        {
            Pawn pawn = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>(__instance, "pawn");
            if (pawn != null)
            {
                Room room = pawn.GetRoom();
                if (room != null)
                {
                    DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
                }
            }
        }

        private static void JobDriver_Lovin_MakeNewToils_Postfix(JobDriver_Lovin __instance, ref IEnumerable<Toil> __result)
        {
            if (__instance.pawn == null)
            {
                return;
            }

            List<Toil> newToils = new List<Toil>();
            foreach (Toil toil in __result)
            {
                newToils.Add(toil);
                if (toil.defaultCompleteMode == ToilCompleteMode.Delay)
                {
                    newToils.Add(Toils_DoNotDisturb.LockRoomDoors());
                }
            }

            newToils.Add(Toils_DoNotDisturb.UnlockRoomDoors());
            __result = newToils;
        }

        private static void JobDriver_Lovin_Start_Postfix(JobDriver_Lovin __instance)
        {
            Pawn pawn = __instance.pawn;
            Room room = pawn.GetRoom();
            if (room != null && DoNotDisturbUtility.ShouldLockRoom(room, pawn))
            {
                DoNotDisturbUtility.SetRoomDoors(room, forbid: true, pawn.Map);
            }
        }

        private static void JobDriver_Lovin_End_Postfix(JobDriver_Lovin __instance)
        {
            Pawn pawn = __instance.pawn;
            Room room = pawn.GetRoom();
            if (room != null)
            {
                DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
            }
        }

        private static void JobDriver_RelaxAlone_MakeNewToils_Postfix(JobDriver_RelaxAlone __instance, ref IEnumerable<Toil> __result)
        {
            if (__instance.pawn == null)
            {
                return;
            }

            List<Toil> newToils = new List<Toil>();
            foreach (Toil toil in __result)
            {
                newToils.Add(toil);
                if (toil.defaultDuration > 0)
                {
                    newToils.Add(Toils_DoNotDisturb.LockRoomDoors());
                }
            }

            newToils.Add(Toils_DoNotDisturb.UnlockRoomDoors());
            __result = newToils;
        }

        private static void JobDriver_RelaxAlone_Start_Postfix(JobDriver_RelaxAlone __instance)
        {
            Pawn pawn = __instance.pawn;
            Room room = pawn.GetRoom();
            if (room != null && DoNotDisturbUtility.ShouldLockRoom(room, pawn))
            {
                DoNotDisturbUtility.SetRoomDoors(room, forbid: true, pawn.Map);
            }
        }

        private static void JobDriver_RelaxAlone_End_Postfix(JobDriver_RelaxAlone __instance)
        {
            Pawn pawn = __instance.pawn;
            Room room = pawn.GetRoom();
            if (room != null)
            {
                DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
            }
        }

        private static void JobDriver_TendPatient_MakeNewToils_Postfix(JobDriver __instance, ref IEnumerable<Toil> __result)
        {
            Pawn patient = __instance.job?.GetTarget(TargetIndex.A).Thing as Pawn;
            if (patient == null)
            {
                return;
            }

            List<Toil> newToils = new List<Toil>();
            newToils.Add(Toils_DoNotDisturb.UnlockDoorsForMedicalTreatment(patient));

            foreach (Toil toil in __result)
            {
                newToils.Add(toil);
            }

            __result = newToils;
        }

        private static void JobDriver_TendPatient_Start_Postfix(JobDriver __instance)
        {
            Pawn doctor = __instance.pawn;
            Pawn patient = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (patient != null)
            {
                Room room = patient.GetRoom();
                if (room != null && DoNotDisturbUtility.ShouldUnlockForMedical(room, patient))
                {
                    DoNotDisturbUtility.SetRoomDoors(room, forbid: false, patient.Map);
                }
            }
        }

        private static IEnumerable<Toil> AddLockToilsToLayDown(JobDriver_LayDown instance, IEnumerable<Toil> baseToils)
        {
            int toilCount = 0;
            foreach (Toil toil in baseToils)
            {
                yield return toil;
                if (toilCount == 1)
                {
                    yield return Toils_DoNotDisturb.LockRoomDoors();
                }
                toilCount++;
            }

            yield return Toils_DoNotDisturb.UnlockRoomDoors();
        }

        private static IEnumerable<Toil> AddLockToilsToLovin(JobDriver_Lovin instance, IEnumerable<Toil> baseToils)
        {
            int toilCount = 0;
            foreach (Toil toil in baseToils)
            {
                yield return toil;
                if (toilCount >= 2 && toil.defaultCompleteMode == ToilCompleteMode.Delay)
                {
                    yield return Toils_DoNotDisturb.LockRoomDoors();
                }
                toilCount++;
            }

            yield return Toils_DoNotDisturb.UnlockRoomDoors();
        }

        private static IEnumerable<Toil> AddLockToilsToRelaxAlone(JobDriver_RelaxAlone instance, IEnumerable<Toil> baseToils)
        {
            int toilCount = 0;
            foreach (Toil toil in baseToils)
            {
                yield return toil;
                if (toilCount >= 1 && toil.defaultDuration > 0)
                {
                    yield return Toils_DoNotDisturb.LockRoomDoors();
                }
                toilCount++;
            }

            yield return Toils_DoNotDisturb.UnlockRoomDoors();
        }

        private static IEnumerable<Toil> AddUnlockToilToTendPatient(JobDriver instance, IEnumerable<Toil> baseToils)
        {
            Pawn patient = instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (patient != null)
            {
                yield return Toils_DoNotDisturb.UnlockDoorsForMedicalTreatment(patient);
            }

            foreach (Toil toil in baseToils)
            {
                yield return toil;
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

                // Use reflection with AccessTools to patch protected methods
                MethodInfo jobDriverNotifyStarting = AccessTools.Method(typeof(JobDriver), "Notify_Starting");
            if (jobDriverNotifyStarting != null)
            {
                harmony.Patch(jobDriverNotifyStarting,
                    prefix: null,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.JobDriver_NotifyStarting_Postfix)));
            }

            MethodInfo pawnJobTrackerEndCurrentJob = AccessTools.Method(typeof(Pawn_JobTracker), "EndCurrentJob");
            if (pawnJobTrackerEndCurrentJob != null)
            {
                harmony.Patch(pawnJobTrackerEndCurrentJob,
                    prefix: null,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Pawn_JobTracker_EndCurrentJob_Postfix)));
            }
            }
            catch (Exception ex)
            {
                Log.Error($"Do Not Disturb :: Failed to apply Harmony patches: {ex}");
            }
        }
    }
}

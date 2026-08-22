using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Do_Not_Disturb
{
    public class Settings : ModSettings
    {
        public static bool EnableChokePointDetection = true;
        public static bool KeepUnlockedForUrgentTending = true;
        public static bool KeepUnlockedForSurgery = true;
        public static bool KeepUnlockedForAnyTending = true;
        public static bool KeepLockedForSoloRelaxation = true;
        public static bool KeepLockedForLovin = false;

        private static readonly Dictionary<Type, Action<Room, Pawn>> LockActions = new Dictionary<Type, Action<Room, Pawn>>
        {
            { typeof(JobDriver_LayDown), LockRoomForSleeping },
            { typeof(JobDriver_Lovin), LockRoomForLovin },
            { typeof(JobDriver_RelaxAlone), LockRoomForSoloRelaxation }
        };

        public static bool ShouldLockFor(JobDriver driver, Room room, Pawn pawn)
        {
            if (driver == null || room == null || pawn == null)
            {
                return false;
            }

            if (LockActions.TryGetValue(driver.GetType(), out Action<Room, Pawn> action))
            {
                return action(room, pawn);
            }

            return false;
        }

        private static bool LockRoomForSleeping(Room room, Pawn pawn)
        {
            return DoNotDisturbUtility.ShouldUnlockForMedical(room, pawn) == false;
        }

        private static bool LockRoomForLovin(Room room, Pawn pawn)
        {
            return KeepLockedForLovin;
        }

        private static bool LockRoomForSoloRelaxation(Room room, Pawn pawn)
        {
            return KeepLockedForSoloRelaxation;
        }

        public static void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard options = new Listing_Standard();

            options.Begin(rect);

            options.Gap(20f);

            options.Label("DND_GeneralOptionsGeneral".Translate());

            options.Gap(10f);

            options.CheckboxLabeled("DND_EnableChokePointDetection".Translate(), ref EnableChokePointDetection, "DND_EnableChokePointDetection_Tooltip".Translate());

            options.Gap(40f);

            options.Label("DND_UnlockOptionsGeneral".Translate());

            options.Gap(10f);

            options.CheckboxLabeled("DND_KeepUnlockedForUrgentTending".Translate(), ref KeepUnlockedForUrgentTending, "DND_KeepUnlockedForUrgentTending_Tooltip".Translate());

            options.Gap(10f);

            options.CheckboxLabeled("DND_KeepUnlockedForSurgery".Translate(), ref KeepUnlockedForSurgery, "DND_KeepUnlockedForSurgery_Tooltip".Translate());

            options.Gap(10f);

            options.CheckboxLabeled("DND_KeepUnlockedForAnyTending".Translate(), ref KeepUnlockedForAnyTending, "DND_KeepUnlockedForAnyTending_Tooltip".Translate());

            options.Gap(40f);

            options.Label("DND_LockOptionsGeneral".Translate());

            options.Gap(10f);

            options.CheckboxLabeled("DND_KeepLockedForSoloRelaxation".Translate(), ref KeepLockedForSoloRelaxation, "DND_KeepLockedForSoloRelaxation_Tooltip".Translate());

            options.Gap(10f);

            options.CheckboxLabeled("DND_KeepLockedForLovin".Translate(), ref KeepLockedForLovin, "DND_KeepLockedForLovin_Tooltip".Translate());

            options.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref EnableChokePointDetection, "DND_EnableChokePointDetection", true);
            Scribe_Values.Look(ref KeepUnlockedForUrgentTending, "DND_KeepUnlockedForUrgentTending", true);
            Scribe_Values.Look(ref KeepUnlockedForSurgery, "DND_KeepUnlockedForSurgery", true);
            Scribe_Values.Look(ref KeepUnlockedForAnyTending, "DND_KeepUnlockedForAnyTending", true);
            Scribe_Values.Look(ref KeepLockedForSoloRelaxation, "DND_KeepLockedForSoloRelaxation", true);
            Scribe_Values.Look(ref KeepLockedForLovin, "DND_KeepLockedForLovin", false);
        }
    }
}

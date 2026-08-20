using UnityEngine;
using Verse;

namespace Do_Not_Disturb
{
    public class Settings : ModSettings
    {
        public static bool EnableChokePointDetection = true;
        public static bool KeepUnlockedForUrgentTending = true;
        public static bool KeepUnlockedForSurgery = true;
        public static bool KeepUnlockedForAnyTending = true;
        public static bool KeepUnlockedForResearch = true;
        public static bool KeepLockedForSoloRelaxation = true;
        public static bool KeepLockedForLovin = false;

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

            options.Gap(10f);

            options.CheckboxLabeled("DND_KeepUnlockedForResearch".Translate(), ref KeepUnlockedForResearch, "DND_KeepUnlockedForResearch_Tooltip".Translate());

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
            Scribe_Values.Look(ref EnableChokePointDetection, "EnableChokePointDetection", true);
            Scribe_Values.Look(ref KeepUnlockedForUrgentTending, "KeepUnlockedForUrgentTending", true);
            Scribe_Values.Look(ref KeepUnlockedForSurgery, "KeepUnlockedForSurgery", true);
            Scribe_Values.Look(ref KeepUnlockedForAnyTending, "KeepUnlockedForAnyTending", true);
            Scribe_Values.Look(ref KeepUnlockedForResearch, "KeepUnlockedForResearch", true);
            Scribe_Values.Look(ref KeepLockedForSoloRelaxation, "KeepLockedForSoloRelaxation", true);
            Scribe_Values.Look(ref KeepLockedForLovin, "KeepLockedForLovin", false);
        }
    }
}

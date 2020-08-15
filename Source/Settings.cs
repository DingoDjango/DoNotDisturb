using UnityEngine;
using Verse;

namespace Do_Not_Disturb
{
	public class Settings : ModSettings
	{
		public static bool KeepUnlockedForUrgentTending = true;

		public static bool KeepUnlockedForSurgery = true;

		public static bool KeepUnlockedForAnyTending = true;

		public static bool KeepLockedForSoloRelaxation = true;

		public static void DoSettingsWindowContents(Rect rect)
		{
			Listing_Standard options = new Listing_Standard();

			options.Begin(rect);

			options.Gap(20f);

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

			options.End();
		}

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref KeepUnlockedForUrgentTending, "DND_KeepUnlockedForUrgentTending", true);
			Scribe_Values.Look(ref KeepUnlockedForSurgery, "DND_KeepUnlockedForSurgery", true);
			Scribe_Values.Look(ref KeepUnlockedForAnyTending, "DND_KeepUnlockedForAnyTending", true);
			Scribe_Values.Look(ref KeepLockedForSoloRelaxation, "DND_KeepLockedForSoloRelaxation", true);
		}
	}
}

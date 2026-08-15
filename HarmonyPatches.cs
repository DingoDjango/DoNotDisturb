using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Do_Not_Disturb
{
	[StaticConstructorOnStartup]
	public static class HarmonyPatches
	{
		private static void Room_Notify_RoomShapeChanged_Postfix(Room __instance)
		{
			ChokePointDetector.InvalidateCache(__instance);
		}

		private static void Pawn_DraftController_Drafted_Postfix(Pawn_DraftController __instance)
		{
			Pawn pawn = __instance.pawn;

			if (pawn.Drafted)
			{
				DoNotDisturbManager manager = pawn.Map.GetComponent<DoNotDisturbManager>();

				// Unforbid doors if a pawn has been drafted, for QoL purposes (otherwise it will take a second)
				manager?.SetRoomDoors(pawn.GetRoom(), false);

#if DEBUG
				Log.Message($"Do Not Disturb :: Unlocked doors for drafted pawn {pawn.Name} with manager {manager.ToString()}");
#endif
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

#if DEBUG
				Log.Message($"Do Not Disturb :: pawnDraftSetter = {pawnDraftSetter.ToString()}");
#endif

				harmony.Patch(pawnDraftSetter,
					prefix: null,
					postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Pawn_DraftController_Drafted_Postfix)));

				MethodInfo roomShapeChanged = AccessTools.Method(typeof(Room), nameof(Room.Notify_RoomShapeChanged));
				harmony.Patch(roomShapeChanged,
					prefix: null,
					postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Room_Notify_RoomShapeChanged_Postfix)));
			}
			catch (Exception ex)
			{
				Log.Error($"Do Not Disturb :: Failed to apply Harmony patches: {ex}");
			}
		}
	}
}

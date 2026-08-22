using System;
using System.Collections.Generic;
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
                DoNotDisturbManager manager = pawn.Map?.GetComponent<DoNotDisturbManager>();
                manager?.SetRoomDoors(pawn.GetRoom(), false);

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
            }
            catch (Exception ex)
            {
                Log.Error($"Do Not Disturb :: Failed to apply Harmony patches: {ex}");
            }
        }
    }
}

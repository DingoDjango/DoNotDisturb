using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Do_Not_Disturb
{
    public static class DoNotDisturbUtility
    {
        public static bool ShouldLockRoom(Room room, Pawn pawn)
        {
            Log.Message($"[DND] ShouldLockRoom check: room={room?.Role.label ?? "NULL"}, pawn={pawn.LabelShort}");
            
            if (room == null || pawn == null)
            {
                Log.Message($"[DND] Room or pawn is null");
                return false;
            }

            if (!room.Owners.Any())
            {
                Log.Message($"[DND] Room has no owners");
                return false;
            }

            Pawn owner = room.Owners.FirstOrDefault();
            if (owner != pawn)
            {
                Log.Message($"[DND] Pawn {pawn.LabelShort} is not the room owner {owner.LabelShort}");
                return false;
            }

            if (ChokePointDetector.IsChokePoint(room))
            {
                Log.Message($"[DND] Room is a choke point, will not lock");
                return false;
            }

            Job job = pawn.CurJob;
            if (job == null)
            {
                Log.Message($"[DND] Pawn has no current job");
                return false;
            }

            Log.Message($"[DND] Pawn job: {job.def.defName}");
            
            if (Settings.KeepLockedForLovin && job.def == JobDefOf.Lovin)
            {
                Log.Message($"[DND] Job is Lovin and KeepLockedForLovin=true → LOCK");
                return true;
            }

            if (Settings.KeepLockedForSoloRelaxation && job.def.driverClass == typeof(JobDriver_RelaxAlone))
            {
                Log.Message($"[DND] Job is RelaxAlone and KeepLockedForSoloRelaxation=true → LOCK");
                return true;
            }

            if (job.def == JobDefOf.LayDown)
            {
                Log.Message($"[DND] Job is LayDown (sleeping/resting) → LOCK");
                return true;
            }

            Log.Message($"[DND] No lock conditions met → NO LOCK");
            return false;
        }

        public static bool ShouldUnlockForMedical(Room room, Pawn patient)
        {
            if (room == null || patient == null)
            {
                return false;
            }

            if (!Settings.KeepUnlockedForAnyTending)
            {
                return false;
            }

            if (patient.health == null)
            {
                return false;
            }

            if (!patient.health.hediffSet.HasTendableHediff())
            {
                return false;
            }

            if (Settings.KeepUnlockedForUrgentTending)
            {
                if (patient.health.HasHediffsNeedingTend())
                {
                    return true;
                }

                if (HasLifeThreateningHediff(patient))
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetRoomDoors(Room room, bool forbid, Map map)
        {
            if (room == null || map == null)
            {
                return;
            }

            List<Thing> containedThings = room.ContainedAndAdjacentThings;
            if (containedThings == null)
            {
                return;
            }

            foreach (Thing thing in containedThings)
            {
                Building_Door door = thing as Building_Door;
                if (door == null || !door.Spawned || door.Map != map)
                {
                    continue;
                }

                DoNotDisturbManager manager = map.GetComponent<DoNotDisturbManager>();
                if (manager != null && !manager.IsDndEnabled(door))
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: Door {door.Label} skipped — DND disabled by player");
#endif
                    continue;
                }

                door.SetForbidden(forbid, warnOnFail: false);

#if DEBUG
                Log.Message($"Do Not Disturb :: Door {door.Label} set forbidden={forbid}");
#endif
            }
        }

        private static bool HasLifeThreateningHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
            {
                return false;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.IsCurrentlyLifeThreatening && !hediff.FullyImmune())
                {
                    return true;
                }
            }

            return false;
        }
    }
}

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
#if DEBUG
            HarmonyPatches.DND_Log($"Should lock room check", new { Room = room?.Role.label ?? "NULL", Pawn = pawn.LabelShort });
#endif
            
            if (room == null || pawn == null)
            {
                return false;
            }

            if (!room.Owners.Any())
            {
                return false;
            }

            Pawn owner = room.Owners.FirstOrDefault();
            if (owner != pawn)
            {
                return false;
            }

            if (ChokePointDetector.IsChokePoint(room))
            {
                return false;
            }

            List<Pawn> containedPawns = room.ContainedThings<Pawn>().ToList();
            if (containedPawns != null)
            {
                foreach (Pawn otherPawn in containedPawns)
                {
                    if (otherPawn != null && !otherPawn.Dead && 
                        otherPawn.IsFreeColonist && 
                        !room.Owners.Contains(otherPawn))
                    {
#if DEBUG
                        Log.Message($"[DND] Non-owner colonist {otherPawn.LabelShort} in room → NO LOCK");
#endif
                        return false;
                    }
                }
            }

            Job job = pawn.CurJob;
            if (job == null)
            {
                return false;
            }

            JobDriver driver = pawn.jobs?.curDriver;
            if (driver == null)
            {
                return false;
            }

#if DEBUG
            HarmonyPatches.DND_Log($"Pawn job check", new { Job = job.def.defName, Driver = driver.GetType().Name });
#endif

            return Settings.ShouldLockFor(driver, room, pawn);
        }

        public static bool ShouldUnlockForMedical(Room room, Pawn patient)
        {
            if (room == null || patient == null)
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

            // Hierarchy: urgent (most restrictive) > surgery > any tending
            
            // Check urgent tending first
            if (Settings.KeepUnlockedForUrgentTending)
            {
                if (HasLifeThreateningHediff(patient))
                {
                    return true;
                }

                if (patient.health.HasHediffsNeedingTend())
                {
                    return true;
                }
            }

            // Check surgery next
            if (Settings.KeepUnlockedForSurgery)
            {
                if (HasScheduledSurgery(patient))
                {
                    return true;
                }
            }

            // Check any tending (least restrictive)
            if (Settings.KeepUnlockedForAnyTending)
            {
                return true;
            }

            return false;
        }

        public static Dictionary<Building_Door, bool> SetRoomDoors(Room room, bool forbid, Map map)
        {
            Dictionary<Building_Door, bool> doorsWithOriginalState = new Dictionary<Building_Door, bool>();

            if (room == null || map == null)
            {
                return doorsWithOriginalState;
            }

            DoNotDisturbManager manager = map.GetComponent<DoNotDisturbManager>();

            // Iterate through room regions and their neighboring door regions
            foreach (Region roomRegion in room.Regions)
            {
                foreach (Region doorRegion in roomRegion.Neighbors)
                {
                    Building_Door door = doorRegion.door;
                    if (door == null)
                    {
                        continue;
                    }

                    if (manager != null && !manager.IsDndEnabled(door))
                    {
#if DEBUG
                        Log.Message($"Do Not Disturb :: Door {door.Label} skipped — DND disabled by player");
#endif
                        continue;
                    }

                    bool shouldForbid = forbid;

                    // When locking: check if would seal off unsynced adjacent rooms
                    if (forbid && AdjacentRoomBlocksLock(doorRegion, room))
                    {
                        shouldForbid = false;
#if DEBUG
                        Log.Message($"Do Not Disturb :: Door {door.Label} kept permitted — adjacent room not in sync");
#endif
                    }

                    // When unlocking: check if adjacent room wanted lock
                    if (!forbid && AdjacentRoomWantedLock(doorRegion, room))
                    {
                        shouldForbid = true;
#if DEBUG
                        Log.Message($"Do Not Disturb :: Door {door.Label} kept forbidden — adjacent room wanted lock");
#endif
                    }

                    bool originalState = door.IsForbidden(Faction.OfPlayer);
                    door.SetForbidden(shouldForbid, warnOnFail: false);
                    doorsWithOriginalState[door] = originalState;
#if DEBUG
                    Log.Message($"Do Not Disturb :: Door {door.Label} set forbidden={shouldForbid} (was {originalState})");
#endif
                }
            }

            return doorsWithOriginalState;
        }

        private static bool AdjacentRoomBlocksLock(Region doorRegion, Room currentRoom)
        {
            foreach ((Room regionRoom, Region region) in FindAdjacentRooms(doorRegion, currentRoom))
            {
                if (regionRoom != null && regionRoom != currentRoom && regionRoom.ProperRoom)
                {
                    if (HasOutsideAccess(regionRoom))
                    {
                        continue;
                    }

                    bool adjacentWantsLock = false;
                    foreach (Pawn pawn in regionRoom.Owners)
                    {
                        if (pawn != null && ShouldLockRoom(regionRoom, pawn))
                        {
                            adjacentWantsLock = true;
                            break;
                        }
                    }

                    if (!adjacentWantsLock)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<(Room, Region)> FindAdjacentRooms(Region doorRegion, Room currentRoom)
        {
            foreach (Region region in doorRegion.Neighbors)
            {
                if (region != null && region.Room != currentRoom)
                {
                    yield return (region.Room, region);
                }
            }
        }

        private static bool HasOutsideAccess(Room room)
        {
            if (room == null || room.Regions == null)
            {
                return false;
            }

            foreach (Region region in room.Regions)
            {
                if (region != null && region.door == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AdjacentRoomWantedLock(Region doorRegion, Room currentRoom)
        {
            foreach ((Room regionRoom, Region region) in FindAdjacentRooms(doorRegion, currentRoom))
            {
                if (regionRoom != null && regionRoom != currentRoom && regionRoom.ProperRoom)
                {
                    foreach (Pawn pawn in regionRoom.Owners)
                    {
                        if (pawn != null && ShouldLockRoom(regionRoom, pawn))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
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

        private static bool HasScheduledSurgery(Pawn pawn)
        {
            if (pawn?.health?.surgeryBills == null)
            {
                return false;
            }

            return pawn.health.surgeryBills.Count > 0;
        }
    }
}

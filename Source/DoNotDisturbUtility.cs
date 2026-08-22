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
                // Check if medical unlock conditions override lock
                if (ShouldUnlockForMedical(room, pawn))
                {
                    Log.Message($"[DND] Job is LayDown but medical unlock conditions met → NO LOCK");
                    return false;
                }

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

        public static void SetRoomDoors(Room room, bool forbid, Map map)
        {
            if (room == null || map == null)
            {
                return;
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

                    door.SetForbidden(shouldForbid, warnOnFail: false);
#if DEBUG
                    Log.Message($"Do Not Disturb :: Door {door.Label} set forbidden={shouldForbid}");
#endif
                }
            }
        }

        private static bool AdjacentRoomBlocksLock(Region doorRegion, Room currentRoom)
        {
            // When current room wants to lock:
            // - If adjacent room is isolated (dead-end with no outside access), it must also want lock
            // - If adjacent room has outside access, it's free to do what it wants
            
            Queue<Region> queue = new Queue<Region>();
            HashSet<Region> visited = new HashSet<Region>();
            queue.Enqueue(doorRegion);
            visited.Add(doorRegion);

            while (queue.Count > 0)
            {
                Region region = queue.Dequeue();
                Room regionRoom = region.Room;

                if (regionRoom != null && regionRoom != currentRoom && regionRoom.ProperRoom)
                {
                    // Skip rooms with outside access (they can always access outside)
                    if (HasOutsideAccess(regionRoom))
                    {
                        continue;
                    }

                    // Check if this isolated adjacent room wants to lock
                    bool adjacentWantsLock = false;
                    foreach (Pawn pawn in regionRoom.Owners)
                    {
                        if (pawn != null && ShouldLockRoom(regionRoom, pawn))
                        {
                            adjacentWantsLock = true;
                            break;
                        }
                    }

                    // If isolated room doesn't want lock → block our lock
                    if (!adjacentWantsLock)
                    {
                        return true;
                    }
                }

                // Continue BFS
                foreach (Region neighbor in region.Neighbors)
                {
                    if (!visited.Contains(neighbor) && neighbor.Room != currentRoom)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return false;
        }

        private static bool HasOutsideAccess(Room room)
        {
            // Check if room has direct access to outside
            if (room == null || room.Regions == null)
            {
                return false;
            }

            foreach (Region region in room.Regions)
            {
                if (region.door == null)  // Open region without door = outside access
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AdjacentRoomWantedLock(Region doorRegion, Room currentRoom)
        {
            // When unlocking: check if adjacent room currently wants lock
            // If so, don't unlock this door (respect their lock preference)
            
            Queue<Region> queue = new Queue<Region>();
            HashSet<Region> visited = new HashSet<Region>();
            queue.Enqueue(doorRegion);
            visited.Add(doorRegion);

            while (queue.Count > 0)
            {
                Region region = queue.Dequeue();
                Room regionRoom = region.Room;

                if (regionRoom != null && regionRoom != currentRoom && regionRoom.ProperRoom)
                {
                    foreach (Pawn pawn in regionRoom.Owners)
                    {
                        if (pawn != null && ShouldLockRoom(regionRoom, pawn))
                        {
                            return true;  // Adjacent room wants lock
                        }
                    }
                }

                foreach (Region neighbor in region.Neighbors)
                {
                    if (!visited.Contains(neighbor) && neighbor.Room != currentRoom)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
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

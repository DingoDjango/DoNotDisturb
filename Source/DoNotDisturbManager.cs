using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Do_Not_Disturb
{
    public class DoNotDisturbManager : MapComponent
    {
        private enum LockState
        {
            Untouched,
            WantLock,
            WantUnlock
        }

        private readonly Dictionary<Room, LockState> RoomState = new Dictionary<Room, LockState>();

        // Per-door DND enabled/disabled storage
        // Door NOT in dict = enabled (default ON)
        // Door in dict with value=false = manually disabled by player
        private readonly Dictionary<Building_Door, bool> doorDisabledDict = new Dictionary<Building_Door, bool>();

        public bool IsDndEnabled(Building_Door door)
        {
            if (door == null) return true;
            if (this.doorDisabledDict.TryGetValue(door, out bool enabled))
            {
                return enabled;
            }
            return true; // Default ON
        }

        public void SetDndEnabled(Building_Door door, bool enabled)
        {
            if (door == null) return;
            if (enabled)
            {
                // Default state — remove from dict to keep it compact
                this.doorDisabledDict.Remove(door);
            }
            else
            {
                this.doorDisabledDict[door] = false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Persist only disabled doors (compact)
                this.scribeDisabledDoors = this.doorDisabledDict.Keys.ToList();
                Scribe_Collections.Look(ref this.scribeDisabledDoors, "dndDisabledDoors", LookMode.Reference);
            }
            else
            {
                Scribe_Collections.Look(ref this.scribeDisabledDoors, "dndDisabledDoors", LookMode.Reference);
                this.doorDisabledDict.Clear();
                if (this.scribeDisabledDoors != null)
                {
                    foreach (Building_Door door in this.scribeDisabledDoors)
                    {
                        if (door != null)
                        {
                            this.doorDisabledDict[door] = false;
                        }
                    }
                }
            }
        }

        private List<Building_Door> scribeDisabledDoors;

        private void RefreshRoomState(Room room, Pawn pawn)
        {
            if (!this.RoomState.TryGetValue(room, out LockState roomState))
            {
                this.RoomState[room] = LockState.Untouched;
            }

            LockState pawnState = this.DetermineLockState(room, pawn);
            if (pawnState > roomState)
            {
                this.RoomState[room] = pawnState;
            }
        }

        private LockState DetermineLockState(Room room, Pawn pawn)
        {
            List<Pawn> owners = room.Owners.ToList();

            // FIRST: If any non-owner colonist is inside, unlock so they can leave
            // Animals excluded — pets don't count as "trapped colonists"
            foreach (Pawn p in room.ContainedAndAdjacentThings.OfType<Pawn>())
            {
                if (!p.Dead && !owners.Contains(p) && p.Faction == Faction.OfPlayer && p.RaceProps != null && !p.RaceProps.Animal)
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantUnlock (non-owner {p.LabelShort} inside)");
#endif
                    return LockState.WantUnlock;
                }
            }

            if ((!owners.Contains(pawn)) ||
                (pawn.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) ||
                (pawn.needs.joy.CurCategory <= JoyCategory.Low))
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantUnlock (pawn={pawn.LabelShort}, not owner/hungry/low joy)");
#endif
                return LockState.WantUnlock;
            }

            foreach (Pawn owner in owners)
            {
                if (owner.GetRoom() != room)
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantUnlock (co-owner {owner.LabelShort} left)");
#endif
                    return LockState.WantUnlock;
                }
            }

            if (pawn.GetRoom() != room)
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantUnlock (pawn not in room)");
#endif
                return LockState.WantUnlock;
            }

            if (Settings.KeepUnlockedForResearch && pawn.CurJob?.def == JobDefOf.Research)
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantUnlock (researching)");
#endif
                return LockState.WantUnlock;
            }

            if (owners.Contains(pawn))
            {
                if (this.IsActuallyResting(pawn) && !this.KeepRoomUnlockedForTending(pawn))
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantLock (resting, pawn={pawn.LabelShort})");
#endif
                    return LockState.WantLock;
                }

                if (Settings.KeepLockedForSoloRelaxation &&
                    pawn.CurJob?.def.driverClass == typeof(JobDriver_RelaxAlone))
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantLock (solo relaxation, pawn={pawn.LabelShort})");
#endif
                    return LockState.WantLock;
                }

                if (Settings.KeepLockedForLovin &&
                    pawn.CurJob?.def == JobDefOf.Lovin)
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} → WantLock (lovin', pawn={pawn.LabelShort})");
#endif
                    return LockState.WantLock;
                }
            }

            return LockState.WantUnlock;
        }

        private bool KeepRoomUnlockedForTending(Pawn pawn)
        {
            if (Settings.KeepUnlockedForUrgentTending)
            {
                if (HealthAIUtility.ShouldBeTendedNowByPlayerUrgent(pawn))
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {pawn.Name} needs urgent tending → unlock");
#endif
                    return true;
                }
                if (HasLifeThreateningHediff(pawn))
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {pawn.Name} has life-threatening hediff → unlock");
#endif
                    return true;
                }
            }
            if (Settings.KeepUnlockedForSurgery && HealthAIUtility.ShouldHaveSurgeryDoneNow(pawn))
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: {pawn.Name} needs surgery → unlock");
#endif
                return true;
            }
            if (Settings.KeepUnlockedForAnyTending && HealthAIUtility.ShouldBeTendedNowByPlayer(pawn))
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: {pawn.Name} needs tending → unlock");
#endif
                return true;
            }

            return false;
        }

        private bool HasLifeThreateningHediff(Pawn pawn)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.IsCurrentlyLifeThreatening && !hediff.FullyImmune())
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: {pawn.Name} has life-threatening hediff: {hediff.Label}");
#endif
                    return true;
                }
            }
            return false;
        }

        private bool IsActuallyResting(Pawn pawn)
        {
            if (pawn.InBed())
            {
                return true;
            }

            if (pawn.CurJob?.def == JobDefOf.LayDown && pawn.GetPosture().Laying())
            {
                return true;
            }

            return false;
        }

        public override void MapComponentTick()
        {
            if ((Find.TickManager.TicksGame % GenTicks.TicksPerRealSecond) != 0)
            {
                return;
            }

            this.RoomState.Clear();

            foreach (Room room in this.map.regionGrid.AllRooms)
            {
                List<Pawn> owners = room.Owners.ToList();
                if (owners.Count == 0)
                {
                    continue;
                }

                // If no owner is present in room, use first owner for state determination
                Pawn pawn = owners.FirstOrDefault(o => o.GetRoom() == room) ?? owners[0];
                this.RefreshRoomState(room, pawn);
            }

            foreach (KeyValuePair<Room, LockState> keyPair in this.RoomState)
            {
                Room room = keyPair.Key;
                LockState state = keyPair.Value;

                if (state == LockState.WantUnlock)
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: Processing {room.Role.label} #{room.ID} WantUnlock → SetRoomDoors(false)");
#endif
                    this.SetRoomDoors(room, false);
                }
                else if (state == LockState.WantLock)
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: Processing {room.Role.label} #{room.ID} WantLock → checking conditions");
#endif
                    if (!ChokePointDetector.IsChokePoint(room))
                    {
#if DEBUG
                        Log.Message($"Do Not Disturb :: OK {room.Role.label} #{room.ID} → SetRoomDoors(true)");
#endif
                        this.SetRoomDoors(room, true);
                    }
#if DEBUG
                    else
                    {
                        Log.Message($"Do Not Disturb :: SKIP {room.Role.label} #{room.ID} lock — choke-point");
                    }
#endif
                }
            }
        }

        private bool AdjacentRoomWantsLock(Region startRegion, Room currentRoom)
        {
            Queue<Region> queue = new Queue<Region>();
            HashSet<Region> visited = new HashSet<Region>();
            queue.Enqueue(startRegion);
            visited.Add(startRegion);

            while (queue.Count > 0)
            {
                Region region = queue.Dequeue();
                Room regionRoom = region.Room;
                if (regionRoom != null &&
                    regionRoom != currentRoom &&
                    regionRoom.ProperRoom &&
                    this.RoomState.TryGetValue(regionRoom, out LockState state) &&
                    state == LockState.WantLock &&
                    !ChokePointDetector.IsChokePoint(regionRoom))
                {
                    return true;
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

        public void SetRoomDoors(Room room, bool forbidDoors)
        {
            foreach (Region roomRegion in room.Regions)
            {
                foreach (Region doorRegion in roomRegion.Neighbors)
                {
                    Building_Door door = doorRegion.door;
                    if (door == null)
                    {
                        continue;
                    }

                    // Skip doors where player has disabled DND
                    if (!this.IsDndEnabled(door))
                    {
#if DEBUG
                        Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} door #{door.thingIDNumber} skipped — DND disabled by player");
#endif
                        continue;
                    }

                    if (!forbidDoors)
                    {
                        // When unlocking, respect adjacent rooms that want lock
                        // BFS through unroomed passages to find actual room beyond
                        if (this.AdjacentRoomWantsLock(doorRegion, room))
                        {
#if DEBUG
                            Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} door kept forbidden — adjacent room wants lock");
#endif
                            continue;
                        }
                    }

                    door.SetForbidden(forbidDoors, false);
                }
            }
#if DEBUG
            Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} doors → {(forbidDoors ? "forbidden" : "permitted")}");
#endif
        }

        public DoNotDisturbManager(Map map) : base(map)
        {
        }
    }
}

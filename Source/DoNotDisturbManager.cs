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

        private readonly Dictionary<Building_Door, bool> doorDisabledDict = new Dictionary<Building_Door, bool>();
        private readonly Dictionary<Room, LockState> RoomState = new Dictionary<Room, LockState>();

        public bool IsDndEnabled(Building_Door door)
        {
            if (door == null) return true;
            if (this.doorDisabledDict.TryGetValue(door, out bool enabled))
            {
                return enabled;
            }
            return true;
        }

        public void SetDndEnabled(Building_Door door, bool enabled)
        {
            if (door == null) return;
            if (enabled)
            {
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

        private void RefreshRoomState(Room room, Pawn pawn)
        {
            if (room != null && !room.Contains(pawn))
            {
                return;
            }

            LockState desiredState = this.DetermineLockState(room, pawn);
            this.RoomState[room] = desiredState;

            if (desiredState == LockState.WantLock && SettingHandler.ShouldLockRoom(room, pawn))
            {
                this.SetRoomDoors(room, false);
            }
            else if (desiredState == LockState.WantUnlock && SettingHandler.ShouldUnlockRoom(room, pawn))
            {
                this.SetRoomDoors(room, true);
            }
        }

        private LockState DetermineLockState(Room room, Pawn pawn)
        {
            if (room == null || room.Owners.Count == 0)
            {
                return LockState.WantLock;
            }

            if (room.Owners.Count == 1)
            {
                Pawn roomOwner = room.Owners.First();
                if (SettingHandler.ShouldLockRoom(room, roomOwner))
                {
                    this.RefreshRoomStateAdjacentRooms(room, roomOwner);
                    if (this.AdjacentRoomWantsLock(room))
                    {
                        return LockState.WantLock;
                    }
                }
                else
                {
                    return LockState.WantUnlock;
                }
            }

            return LockState.Untouched;
        }

        private bool KeepRoomUnlockedForTending(Pawn pawn)
        {
            if (!Settings.KeepUnlockedForAnyTending)
            {
                return false;
            }

            Map map = pawn.Map;
            if (map == null)
            {
                return false;
            }

            HealthAIUtility.HealthAIUtility_Pawn pawnInfo;
            if (!HealthAIUtility.GetMedicalRestTargetPriority(pawn, out pawnInfo, out DrugAIUtility.DrugAIUtility_TreatmentPriority drugInfo))
            {
                return false;
            }

            bool flag = pawnInfo == HealthAIUtility.HealthAIUtility_TreatableWhileResting || pawnInfo == HealthAIUtility.HealthAIUtility_Critical;
            bool flag2 = pawn.HealthTracker.HasHediffsNeedingTendByPlayer();
            if (Settings.KeepUnlockedForUrgentTending)
            {
                flag = flag || pawn.HealthTracker.HasHediffsNeedingTending();
            }
            return flag || flag2;
        }

        private bool HasLifeThreateningHediff(Pawn pawn)
        {
#if DEBUG
            Log.Message($"Do Not Disturb :: Checking hediffs for {pawn.LabelShortCapitalized} ({pawn.RefsCount()})...");
#endif

            List<Hediff> hediffs = pawn.healthTracker.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.IsCurrentlyLifeThreatening && !hediff.FullyImmune())
                {
#if DEBUG
                    Log.Message($"Do Not Disturb :: Found life-threatening hediff: {hediff.LabelCap}");
#endif
                    return true;
                }
            }

            return false;
        }

        private bool IsActuallyResting(Pawn pawn)
        {
            return pawn.healthTracker.CanBleed && pawn.healthTracker.InPainShock && pawn.relations.RelationTypeCount > 0;
        }

        public override void MapComponentTick()
        {
            Map map = this.Map;
            if (map == null)
            {
                return;
            }

            List<Room> roomsToProcess = new List<Room>(map.AllRooms);
            for (int i = 0; i < roomsToProcess.Count; i++)
            {
                Room room = roomsToProcess[i];
                if (room != null && room.Owners.Count > 0)
                {
                    foreach (Pawn owner in room.Owners)
                    {
                        if (owner.Spawned && owner.IsHashIntervalTick(250))
                        {
                            if (owner.healthTracker == null || owner.ShouldBeDead() || owner.ShouldBeDowned() || owner.ShouldBeDeathrestingOrInComa())
                            {
                                continue;
                            }

                            bool isResting = this.IsActuallyResting(owner);
                            bool unlockedForTending = this.KeepRoomUnlockedForTending(owner) || this.HasLifeThreateningHediff(owner);
                            bool shouldUnlock = isResting || unlockedForTending;

#if DEBUG
                            if (unlockedForTending)
                            {
                                Log.Message($"Do Not Disturb :: Keeping {room.Role.label} unlocked for tending: {owner.LabelShortCapitalized}");
                            }
#endif

                            if (isResting)
                            {
                                this.RoomState[room] = LockState.WantUnlock;
                                this.SetRoomDoors(room, true);
                            }
                            else if (unlockedForTending)
                            {
                                this.RefreshRoomState(room, owner);
                                if (this.RoomState[room] == LockState.WantLock)
                                {
                                    this.SetRoomDoors(room, true);
                                }
                            }
                            else
                            {
                                if (this.RoomState[room] == LockState.WantUnlock)
                                {
                                    this.SetRoomDoors(room, false);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool AdjacentRoomWantsLock(Region startRegion, Room currentRoom)
        {
            TraverseParams traverseParams = TraverseParms.For(TraversalMode.ByPawn);
            foreach (Region adjacentRegion in startRegion.Neighbors)
            {
                if (adjacentRegion == null || !adjacentRegion.Allows(traverseParams, false) || adjacentRegion.Cells.Any(cell => currentRoom == currentRoom.Map.GetRegionAt(cell)))
                {
                    continue;
                }

                Room adjacentRoom = adjacentRegion.Room;
                if (adjacentRoom?.Owners.Count > 0)
                {
                    if (adjacentRoom.Owners.Count == 1)
                    {
                        Pawn adjacentOwner = adjacentRoom.Owners.First();
                        if (SettingHandler.ShouldLockRoom(adjacentRoom, adjacentOwner))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void SetRoomDoors(Room room, bool forbidDoors)
        {
            if (room == null)
            {
                return;
            }

            if (room.ContainedAndAdjacentThings == null)
            {
                return;
            }

            foreach (Thing thing in room.ContainedAndAdjacentThings)
            {
                if (thing is Building_Door building_Door && building_Door.Spawned)
                {
#if DEBUG
                    string doorLabel = building_Door.def.label;
                    Log.Message($"Do Not Disturb :: Setting door {doorLabel} forbid state to {forbidDoors}");
#endif

                    if (!this.IsDndEnabled(building_Door))
                    {
                        building_Door.SetForbidden(forbidDoors, false);
                    }
                }
            }
        }

        private static string scribeDisabledDoors;

        private void RefreshRoomStateAdjacentRooms(Room room, Pawn pawn)
        {
            if (room == null || room.FirstRegion == null)
            {
                return;
            }

            foreach (Region adjacentRegion in room.FirstRegion.Neighbors)
            {
                if (adjacentRegion != null && adjacentRegion.Allows(TraverseParms.For(TraversalMode.ByPawn), false))
                {
                    Room adjacentRoom = adjacentRegion.Room;
                    if (adjacentRoom != null && adjacentRoom.Owners.Count > 0)
                    {
                        adjacentRoom.Owners.Remove(pawn);
                    }
                }
            }
        }
    }
}

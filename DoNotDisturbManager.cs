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
        private readonly Dictionary<Room, int> UnlockGraceTick = new Dictionary<Room, int>();
        private const int UnlockGraceTicks = 120; // 2 seconds at 60 ticks/sec

        private LockState RoomStateForPawn(Room room, Pawn pawn)
        {
            List<Pawn> owners = room.Owners.ToList();

            if ((!owners.Contains(pawn)) ||
                (pawn.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) ||
                (pawn.needs.joy.CurCategory <= JoyCategory.Low))
            {
                return LockState.WantUnlock;
            }

            foreach (Pawn owner in owners)
            {
                if (owner.GetRoom() != room)
                {
                    return LockState.WantUnlock;
                }
            }

            if (pawn.GetRoom() != room)
            {
                return LockState.WantUnlock;
            }

            if (Settings.KeepUnlockedForResearch && pawn.CurJob?.def == JobDefOf.Research)
            {
                return LockState.WantUnlock;
            }

            if (pawn.InBed() && !this.KeepRoomUnlockedForTending(pawn))
            {
                return LockState.WantLock;
            }

            if (Settings.KeepLockedForSoloRelaxation && (pawn.CurJob?.def.driverClass == typeof(JobDriver_RelaxAlone)))
            {
                return LockState.WantLock;
            }

            return LockState.WantUnlock;
        }

        private bool KeepRoomUnlockedForTending(Pawn patient)
        {
            if ((Settings.KeepUnlockedForUrgentTending && HealthAIUtility.ShouldBeTendedNowByPlayerUrgent(patient))
                || (Settings.KeepUnlockedForSurgery && HealthAIUtility.ShouldHaveSurgeryDoneNow(patient))
                || (Settings.KeepUnlockedForAnyTending && HealthAIUtility.ShouldBeTendedNowByPlayer(patient)))
            {
                return true;
            }

            return false;
        }

        private void RefreshRoomState(Room room, Pawn pawn)
        {
            if (!this.RoomState.TryGetValue(room, out LockState roomLockState))
            {
                this.RoomState[room] = LockState.Untouched;
            }

            LockState pawnState = this.RoomStateForPawn(room, pawn);

            if (pawnState > roomLockState)
            {
                this.RoomState[room] = pawnState;
            }
        }

        //TODO: what about doors to adjacent rooms / hallways?
        public void SetRoomDoors(Room room, bool forbidDoors)
        {
            foreach (Region roomRegion in room.Regions)
            {
                foreach (Region doorRegion in roomRegion.Neighbors)
                {
                    doorRegion.door?.SetForbidden(forbidDoors, false);
                }
            }

            if (!forbidDoors)
            {
                this.UnlockGraceTick[room] = Find.TickManager.TicksGame + UnlockGraceTicks;
            }
        }

        public override void MapGenerated()
        {
            this.RoomState.Clear();
            this.UnlockGraceTick.Clear();
        }

        public override void MapRemoved()
        {
            this.RoomState.Clear();
            this.UnlockGraceTick.Clear();
        }

        public override void MapComponentTick()
        {
            if ((Find.TickManager.TicksGame % GenTicks.TicksPerRealSecond) == 0)
            {
                this.RoomState.Clear();

                IReadOnlyList<Room> allRooms = this.map.regionGrid.AllRooms;
                for (int i = 0; i < allRooms.Count; i++)
                {
                    Room room = allRooms[i];

                    List<Pawn> owners = room.Owners.ToList();
                    if (owners.Count == 0)
                        continue;

                    for (int j = 0; j < owners.Count; j++)
                    {
                        Pawn owner = owners[j];
                        if (owner.GetRoom() == room)
                        {
                            this.RefreshRoomState(room, owner);
                        }
                    }
                }

                foreach (KeyValuePair<Room, LockState> keyPair in this.RoomState)
                {
                    Room room = keyPair.Key;
                    LockState state = keyPair.Value;

                    if (this.UnlockGraceTick.TryGetValue(room, out int graceUntil) &&
                        Find.TickManager.TicksGame < graceUntil)
                    {
                        continue;
                    }

                    if (state == LockState.WantUnlock)
                    {
                        this.SetRoomDoors(room, false);
                        this.UnlockGraceTick[room] = Find.TickManager.TicksGame + UnlockGraceTicks;
                    }
                    else if (state == LockState.WantLock)
                    {
                        this.SetRoomDoors(room, true);
                    }
                }
            }
        }

        // Required constructor
        public DoNotDisturbManager(Map map) : base(map)
        {
        }
    }
}

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

        // Grace period after a room is unlocked: don't re-lock for this many ticks.
        // Prevents the draft-toggle loop where a room gets unblocked then immediately
        // re-locked before pawns can traverse through it.
        private readonly Dictionary<Room, int> UnlockGraceTick = new Dictionary<Room, int>();
        private const int UnlockGraceTicks = 120; // 2 seconds at 60 ticks/sec

        private LockState RoomStateForPawn(Room room, Pawn pawn)
        {
            // Cache owners to a single enumeration — avoids double-iterating the yield-return enumerable
            List<Pawn> owners = room.Owners.ToList();

            if ((!owners.Contains(pawn)) ||
                (pawn.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) ||
                (pawn.needs.joy.CurCategory <= JoyCategory.Low))
            {
                /* Pawn: doesn't own this room / is starving / needs joy
                 * Result: keep room unlocked */

                return LockState.WantUnlock;
            }

            // Bug fix: if any owner is outside this room, keep it unlocked
            // so they can enter. This prevents trapping owners who need
            // to reach their own bedroom.
            foreach (Pawn owner in owners)
            {
                if (owner.GetRoom() != room)
                {
                    /* Not all of the room's owners are in it
                     * Result: keep room unlocked */

                    return LockState.WantUnlock;
                }
            }

            // Bug fix: if the pawn is outside this room but the room is locked,
            // the room must stay unlocked. Without this, a pawn with an urgent
            // need who is currently outside the room would create a deadlock:
            // need stays urgent → always WantUnlock, but the door is already
            // locked and the pawn can't get in to relieve the need.
            if (pawn.GetRoom() != room)
            {
                return LockState.WantUnlock;
            }

            if (pawn.InBed() && !this.KeepRoomUnlockedForTending(pawn))
            {
                /* Pawn: in bed, does not require doctor care
                 * Result: lock the room */

                return LockState.WantLock;
            }

            if (Settings.KeepLockedForSoloRelaxation && (pawn.CurJob?.def.driverClass == typeof(JobDriver_RelaxAlone)))
            {
                /* Pawn: is relaxing alone, requires privacy
                 * Result: lock the room */

                return LockState.WantLock;
            }

            /* Pawn: is awake, does not require privacy
             * Result: keep room unlocked */

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

            /* Pawn needs take precedent over cached room state
			 * Result: always unlock if any pawn in the room requires it */
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
                    // Lock or unlock doors in neighbouring regions
                    doorRegion.door?.SetForbidden(forbidDoors, false);
                }
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
            // Triggers once per second (60 ticks)
            if ((Find.TickManager.TicksGame % GenTicks.TicksPerRealSecond) == 0)
            {
                this.RoomState.Clear();

                // Iterate rooms directly via regionGrid.AllRooms instead of per-pawn.
                // This avoids visiting the same room multiple times when multiple
                // colonists share it, and uses the game's own room enumeration.
                IReadOnlyList<Room> allRooms = this.map.regionGrid.AllRooms;
                for (int i = 0; i < allRooms.Count; i++)
                {
                    Room room = allRooms[i];

                    // Enumerate owners once — Any() short-circuits but ToList() re-enumerates,
                    // so combine into a single pass to avoid double-work on Room.Owners yield-return.
                    List<Pawn> owners = room.Owners.ToList();
                    if (owners.Count == 0)
                        continue;

                    // Evaluate each owner pawn in the room
                    for (int j = 0; j < owners.Count; j++)
                    {
                        Pawn owner = owners[j];
                        // Only evaluate if the owner is actually in this room
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

                    // Check grace period: if room was recently unlocked, skip re-lock
                    if (this.UnlockGraceTick.TryGetValue(room, out int graceUntil) &&
                        Find.TickManager.TicksGame < graceUntil)
                    {
                        continue;
                    }

                    if (state == LockState.WantUnlock)
                    {
                        // Unlock room — start grace period to prevent immediate re-lock
                        this.SetRoomDoors(room, false);
                        this.UnlockGraceTick[room] = Find.TickManager.TicksGame + UnlockGraceTicks;
                    }
                    else if (state == LockState.WantLock)
                    {
                        // Lock room
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

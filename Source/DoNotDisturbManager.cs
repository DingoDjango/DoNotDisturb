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

        private const int UnlockGraceTicks = 120; // 2 seconds at 60 ticks/sec
        private readonly Dictionary<Room, LockState> RoomState = new Dictionary<Room, LockState>();
        private readonly Dictionary<Room, int> UnlockGraceTick = new Dictionary<Room, int>();

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
            return (Settings.KeepUnlockedForUrgentTending && HealthAIUtility.ShouldBeTendedNowByPlayerUrgent(pawn)) ||
                   (Settings.KeepUnlockedForSurgery && HealthAIUtility.ShouldHaveSurgeryDoneNow(pawn)) ||
                   (Settings.KeepUnlockedForAnyTending && HealthAIUtility.ShouldBeTendedNowByPlayer(pawn));
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
                foreach (Pawn owner in room.Owners)
                {
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
#if DEBUG
                    Log.Message($"Do Not Disturb :: Processing {room.Role.label} #{room.ID} WantUnlock → SetRoomDoors(false)");
#endif
                    this.SetRoomDoors(room, false);
                    this.UnlockGraceTick[room] = Find.TickManager.TicksGame + UnlockGraceTicks;
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

        public void SetRoomDoors(Room room, bool forbidDoors)
        {
            foreach (Region roomRegion in room.Regions)
            {
                foreach (Region doorRegion in roomRegion.Neighbors)
                {
                    doorRegion.door?.SetForbidden(forbidDoors, false);
                }
            }
#if DEBUG
            Log.Message($"Do Not Disturb :: {room.Role.label} #{room.ID} doors → {(forbidDoors ? "forbidden" : "permitted")}");
#endif

            if (!forbidDoors)
            {
                this.UnlockGraceTick[room] = Find.TickManager.TicksGame + UnlockGraceTicks;
            }
        }

        public DoNotDisturbManager(Map map) : base(map)
        {
        }
    }
}

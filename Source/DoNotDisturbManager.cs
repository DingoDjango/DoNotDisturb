using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Do_Not_Disturb
{
    public class DoNotDisturbManager : MapComponent
    {
        private List<Building_Door> scribeDisabledDoors;
        private readonly Dictionary<Building_Door, bool> doorDisabledDict = new Dictionary<Building_Door, bool>();
        private readonly Dictionary<int, Room> pawnsDndRooms = new Dictionary<int, Room>();
        private readonly Dictionary<int, Dictionary<Building_Door, bool>> pawnsDndDoors = new Dictionary<int, Dictionary<Building_Door, bool>>();

        public DoNotDisturbManager(Map map) : base(map)
        {
        }

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

        public void PawnStartedDnd(Pawn pawn, Room room, Dictionary<Building_Door, bool> doorsWithOriginalState = null)
        {
            if (pawn == null)
            {
                return;
            }

            if (room != null)
            {
                pawnsDndRooms[pawn.thingIDNumber] = room;
            }

            if (doorsWithOriginalState != null && doorsWithOriginalState.Count > 0)
            {
                pawnsDndDoors[pawn.thingIDNumber] = doorsWithOriginalState;
            }
        }

        public void SyncRoomDoorsForJob(JobDriver driver, Room room)
        {
            if (driver == null || room == null)
            {
                return;
            }

            bool lockRoom = DoNotDisturbUtility.ShouldLockRoom(room, driver.pawn);
            DoNotDisturbUtility.SetRoomDoors(room, forbid: lockRoom, driver.pawn.Map);
            HarmonyPatches.DND_Log($"Sync triggered", new { LockState = lockRoom, RoomId = room.ID });
        }

        public void PawnEndedDnd(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            bool wasActive = pawnsDndRooms.Remove(pawn.thingIDNumber) || pawnsDndDoors.Remove(pawn.thingIDNumber);
#if DEBUG
            Log.Message($"[DND] Unregistered pawn {pawn.LabelShort} from DND (was active: {wasActive})");
#endif
        }

        public bool IsPawnDndActive(Pawn pawn)
        {
            return pawn?.IsFreeColonist == true && (pawnsDndRooms.ContainsKey(pawn.thingIDNumber) || pawnsDndDoors.ContainsKey(pawn.thingIDNumber));
        }

        public Room GetDndRoom(Pawn pawn)
        {
            if (pawn != null && pawnsDndRooms.TryGetValue(pawn.thingIDNumber, out Room room))
            {
                return room;
            }

            return null;
        }

        public IEnumerable<Building_Door> GetDndDoors(Pawn pawn)
        {
            if (pawn != null && pawnsDndDoors.TryGetValue(pawn.thingIDNumber, out Dictionary<Building_Door, bool> doors))
            {
                return doors.Keys;
            }

            return Enumerable.Empty<Building_Door>();
        }

        public void ClearDoorFromAllPawns(Building_Door door)
        {
            if (door == null)
            {
                return;
            }

            foreach (Dictionary<Building_Door, bool> doorDict in pawnsDndDoors.Values)
            {
                doorDict.Remove(door);
            }
#if DEBUG
            Log.Message($"[DND] Cleared door {door.Label} from all tracked pawns due to player interaction");
#endif
        }

        public bool GetOriginalDoorState(Pawn pawn, Building_Door door)
        {
            if (pawn != null && door != null && pawnsDndDoors.TryGetValue(pawn.thingIDNumber, out Dictionary<Building_Door, bool> doors))
            {
                if (doors.TryGetValue(door, out bool originalState))
                {
                    return originalState;
                }
            }

            return false;
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
    }
}

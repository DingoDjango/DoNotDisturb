using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Do_Not_Disturb
{
    public class DoNotDisturbManager : MapComponent
    {
        private List<Building_Door> scribeDisabledDoors;
        private readonly Dictionary<Building_Door, bool> doorDisabledDict = new Dictionary<Building_Door, bool>();
        private readonly Dictionary<int, Room> pawnsDndRooms = new Dictionary<int, Room>();

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

        public void PawnStartedDnd(Pawn pawn, Room room)
        {
            if (pawn != null && room != null)
            {
                pawnsDndRooms[pawn.thingIDNumber] = room;
#if DEBUG
                Log.Message($"[DND] Registered pawn {pawn.LabelShort} as DND active");
#endif
            }
        }

        public void PawnEndedDnd(Pawn pawn)
        {
            if (pawn != null)
            {
                bool wasActive = pawnsDndRooms.Remove(pawn.thingIDNumber);
#if DEBUG
                Log.Message($"[DND] Unregistered pawn {pawn.LabelShort} from DND (was active: {wasActive})");
#endif
            }
        }

        public bool IsPawnDndActive(Pawn pawn)
        {
            return pawn != null && pawnsDndRooms.ContainsKey(pawn.thingIDNumber);
        }

        public Room GetDndRoom(Pawn pawn)
        {
            if (pawn != null && pawnsDndRooms.TryGetValue(pawn.thingIDNumber, out Room room))
            {
                return room;
            }

            return null;
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

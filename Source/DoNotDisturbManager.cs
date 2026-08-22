using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Do_Not_Disturb
{
    public class DoNotDisturbManager : MapComponent
    {
        private readonly Dictionary<Building_Door, bool> doorDisabledDict = new Dictionary<Building_Door, bool>();
        private readonly HashSet<int> pawnsDndActive = new HashSet<int>();

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

        public void PawnStartedDnd(Pawn pawn)
        {
            if (pawn != null)
            {
                pawnsDndActive.Add(pawn.thingIDNumber);
                Log.Message($"[DND] Registered pawn {pawn.LabelShort} as DND active");
            }
        }

        public void PawnEndedDnd(Pawn pawn)
        {
            if (pawn != null)
            {
                bool wasActive = pawnsDndActive.Remove(pawn.thingIDNumber);
                Log.Message($"[DND] Unregistered pawn {pawn.LabelShort} from DND (was active: {wasActive})");
            }
        }

        public bool IsPawnDndActive(Pawn pawn)
        {
            return pawn != null && pawnsDndActive.Contains(pawn.thingIDNumber);
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

        private List<Building_Door> scribeDisabledDoors;
    }
}

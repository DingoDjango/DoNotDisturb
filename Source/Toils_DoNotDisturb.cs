using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Do_Not_Disturb
{
    public static class Toils_DoNotDisturb
    {
        public static Toil LockRoomDoors()
        {
            Toil toil = ToilMaker.MakeToil("LockRoomDoors");
            toil.initAction = delegate
            {
                Pawn pawn = toil.actor;
                Room room = pawn.GetRoom();
                
                if (room == null || !room.Owners.Any())
                {
                    return;
                }

                if (!DoNotDisturbUtility.ShouldLockRoom(room, pawn))
                {
                    return;
                }

#if DEBUG
                Log.Message($"[DND] Locking doors for {pawn.LabelShort} in {room.Role.label} (job: {pawn.CurJob?.def.defName ?? "NULL"})");
#endif
                Dictionary<Building_Door, bool> lockedDoors = DoNotDisturbUtility.SetRoomDoors(room, forbid: true, pawn.Map);

                DoNotDisturbManager manager = pawn.Map?.GetComponent<DoNotDisturbManager>();
                if (manager != null)
                {
                    manager.PawnStartedDnd(pawn, room, lockedDoors);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}


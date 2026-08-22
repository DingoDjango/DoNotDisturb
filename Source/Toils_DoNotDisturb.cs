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
                
                Log.Message($"[DND] LockRoomDoors toil executing for {pawn.LabelShort}, room={room?.Role.label ?? "NULL"}");
                
                if (room == null || !room.Owners.Any())
                {
                    Log.Message($"[DND] Room is null or has no owners, skipping lock");
                    return;
                }

                if (!DoNotDisturbUtility.ShouldLockRoom(room, pawn))
                {
                    Log.Message($"[DND] DoNotDisturbUtility.ShouldLockRoom returned false, skipping lock");
                    return;
                }

                Log.Message($"[DND] Locking doors for {pawn.LabelShort} in {room.Role.label}");
                DoNotDisturbUtility.SetRoomDoors(room, forbid: true, pawn.Map);
                
                DoNotDisturbManager manager = pawn.Map?.GetComponent<DoNotDisturbManager>();
                if (manager != null)
                {
                    manager.PawnStartedDnd(pawn);
                }
                
                Log.Message($"[DND] Doors locked");
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil UnlockRoomDoors()
        {
            Toil toil = ToilMaker.MakeToil("UnlockRoomDoors");
            toil.initAction = delegate
            {
                Pawn pawn = toil.actor;
                Room room = pawn.GetRoom();
                
                Log.Message($"[DND] UnlockRoomDoors toil executing for {pawn.LabelShort}, room={room?.Role.label ?? "NULL"}");
                
                DoNotDisturbManager manager = pawn.Map?.GetComponent<DoNotDisturbManager>();
                if (manager != null)
                {
                    manager.PawnEndedDnd(pawn);
                }
                
                if (room == null)
                {
                    Log.Message($"[DND] Room is null when trying to unlock! Pawn position: {pawn.Position}, spawned: {pawn.Spawned}");
                    return;
                }

                Log.Message($"[DND] Unlocking doors for {pawn.LabelShort} in {room.Role.label}");
                DoNotDisturbUtility.SetRoomDoors(room, forbid: false, pawn.Map);
                Log.Message($"[DND] Doors unlocked successfully");
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil UnlockDoorsForMedicalTreatment(Pawn patient)
        {
            Toil toil = ToilMaker.MakeToil("UnlockDoorsForMedicalTreatment");
            toil.initAction = delegate
            {
                if (patient == null)
                {
                    Log.Message($"[DND] UnlockDoorsForMedicalTreatment: patient is null");
                    return;
                }

                Room room = patient.GetRoom();
                Log.Message($"[DND] UnlockDoorsForMedicalTreatment for {patient.LabelShort}, room={room?.Role.label ?? "NULL"}");
                
                if (room == null)
                {
                    Log.Message($"[DND] Patient room is null");
                    return;
                }

                if (!DoNotDisturbUtility.ShouldUnlockForMedical(room, patient))
                {
                    Log.Message($"[DND] ShouldUnlockForMedical returned false");
                    return;
                }

                Log.Message($"[DND] Unlocking doors for medical treatment of {patient.LabelShort}");
                DoNotDisturbUtility.SetRoomDoors(room, forbid: false, patient.Map);
                Log.Message($"[DND] Medical unlock complete");
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}


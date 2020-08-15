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

		private LockState RoomStateForPawn(Room room, Pawn pawn)
		{
			if ((!room.Owners.Contains(pawn)) ||
				(pawn.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) ||
				(pawn.needs.joy.CurCategory <= JoyCategory.Low))
			{
				/* Pawn: doesn't own this room / is starving / needs joy
				 * Result: keep room unlocked */

				return LockState.WantUnlock;
			}

			foreach (Pawn owner in room.Owners)
			{
				if (owner.GetRoom() != room)
				{
					/* Not all of the room's owners are in it
					 * Result: keep room unlocked */

					return LockState.WantUnlock;
				}
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

		public void SetRoomDoors(Room room, bool forbidDoors)
		{
			foreach (Region region in room.Regions)
			{
				foreach (Region doorRegion in region.Neighbors)
				{
					// Lock or unlock doors in neighbouring regions
					doorRegion.door?.SetForbidden(forbidDoors, false);
				}
			}
		}

		public override void MapComponentTick()
		{
			// Triggers once per second (60 ticks)
			if ((Find.TickManager.TicksGame % GenTicks.SecondsToTicks(1f)) == 0)
			{
				if (this.RoomState.Count > 0)
				{
					this.RoomState.Clear();
				}

				List<Pawn> pawns = Find.CurrentMap.mapPawns.FreeColonists;

				if (!pawns.NullOrEmpty())
				{
					// Check if room should be locked or unlocked
					foreach (Pawn pawn in pawns)
					{
						/* Old check for prisoners - seems deprecated...
						 * if (pawn.IsPrisonerOfColony)
						 * {
						 * 	continue;
						 * } */

						Room room = pawn.GetRoom();

						if (room != null && room.Owners.Count() > 0)
						{
							this.RefreshRoomState(room, pawn);
						}
					}

					foreach (KeyValuePair<Room, LockState> keyPair in this.RoomState)
					{
						if (keyPair.Value == LockState.WantUnlock)
						{
							// Unlock room
							this.SetRoomDoors(keyPair.Key, false);
						}
						else if (keyPair.Value == LockState.WantLock)
						{
							// Lock room
							this.SetRoomDoors(keyPair.Key, true);
						}
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

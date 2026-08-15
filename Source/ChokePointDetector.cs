using System.Collections.Generic;
using Verse;

namespace Do_Not_Disturb
{
    public static class ChokePointDetector
    {
        private class CachedChokePointData
        {
            public bool IsChokePoint;
            public int ValidUntilTick;
        }

        private static readonly Dictionary<Room, CachedChokePointData> Cache = new Dictionary<Room, CachedChokePointData>();
#if DEBUG
        private static int CacheHits = 0;
        private static int CacheMisses = 0;
#endif

        public static void InvalidateCache(Room room)
        {
#if DEBUG
            if (Cache.ContainsKey(room))
            {
                Log.Message($"Do Not Disturb :: Choke-point cache invalidated for {room.Role.label} #{room.ID}");
            }
#endif
            Cache.Remove(room);
        }

        public static void ClearCache()
        {
#if DEBUG
            Log.Message($"Do Not Disturb :: Choke-point cache cleared (had {Cache.Count} entries)");
#endif
            Cache.Clear();
        }

        public static bool IsChokePoint(Room room)
        {
            if (!Settings.EnableChokePointDetection)
            {
                return false;
            }

            if (Cache.TryGetValue(room, out CachedChokePointData cached) &&
                Find.TickManager.TicksGame < cached.ValidUntilTick)
            {
#if DEBUG
                CacheHits++;
#endif
                return cached.IsChokePoint;
            }

#if DEBUG
            CacheMisses++;
#endif

            bool result = CalculateIsChokePoint(room);

            Cache[room] = new CachedChokePointData
            {
                IsChokePoint = result,
                ValidUntilTick = Find.TickManager.TicksGame + GenTicks.TicksPerRealSecond * 10
            };

            return result;
        }

#if DEBUG
        public static void LogCacheStats()
        {
            Log.Message($"Do Not Disturb :: Choke-point cache stats — hits: {CacheHits}, misses: {CacheMisses}, size: {Cache.Count}");
        }
#endif

        private static bool CalculateIsChokePoint(Room room)
        {
            HashSet<Room> adjacentRooms = new HashSet<Room>();

            foreach (Region region in room.Regions)
            {
                foreach (Region neighbor in region.Neighbors)
                {
                    if (neighbor.Room != null && neighbor.Room != room && neighbor.door != null)
                    {
                        adjacentRooms.Add(neighbor.Room);
                    }
                }
            }

            if (adjacentRooms.Count < 2)
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: Choke-point check {room.Role.label} #{room.ID}: skipped (only {adjacentRooms.Count} adjacent rooms)");
#endif
                return false;
            }

            List<Room> roomList = new List<Room>(adjacentRooms);
            Room startRoom = roomList[0];
            Region root = startRoom.FirstRegion;

            if (root == null)
            {
#if DEBUG
                Log.Message($"Do Not Disturb :: Choke-point check {room.Role.label} #{room.ID}: skipped (start room has no regions)");
#endif
                return false;
            }

            int reachedCount = 0;
            HashSet<Room> reachedRooms = new HashSet<Room>();

            RegionProcessor processor = delegate (Region r)
            {
                if (r.Room != null && r.Room != startRoom && roomList.Contains(r.Room) && reachedRooms.Add(r.Room))
                {
                    reachedCount++;
                }
                return reachedCount >= roomList.Count - 1;
            };

            RegionEntryPredicate entryCondition = (Region from, Region to) => to.Room != room;

            RegionTraverser.BreadthFirstTraverse(root, entryCondition, processor, 999999, RegionType.Set_Passable);

            bool isChokePoint = reachedCount < roomList.Count - 1;

#if DEBUG
            Log.Message($"Do Not Disturb :: Choke-point check {room.Role.label} #{room.ID}: adjacent={adjacentRooms.Count}, reachable={reachedCount + 1}/{roomList.Count}, isChokePoint={isChokePoint}");
#endif

            return isChokePoint;
        }
    }
}

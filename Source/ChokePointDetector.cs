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
            int validUntilTick = Find.TickManager.TicksGame + 3000;
            Cache[room] = new CachedChokePointData
            {
                IsChokePoint = result,
                ValidUntilTick = validUntilTick
            };
            return result;
        }

        public static void LogCacheStats()
        {
#if DEBUG
            Log.Message($"Do Not Disturb :: Choke-point cache stats - Hits: {CacheHits}, Misses: {CacheMisses}, Hit rate: {(CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) * 100 : 0):F1}%");
#endif
        }

        private static bool CalculateIsChokePoint(Room room)
        {
            if (room == null || !room.ProperRoom)
            {
                return false;
            }

            List<Region> regions = room.Regions;
            if (regions.Count == 0)
            {
                return false;
            }

            foreach (Region region in regions)
            {
                if (region == null)
                {
                    continue;
                }

                int doorCount = 0;
                foreach (RegionLink regionLink in region.links.Links)
                {
                    if (regionLink == null)
                    {
                        continue;
                    }

                    Region otherRegion = regionLink.GetOtherRegion(region);
                    if (otherRegion != null && otherRegion.Room != room)
                    {
                        if (regionLink.RegionA != null && regionLink.RegionA.District != null)
                        {
                            doorCount++;
                        }
                    }
                }

                if (doorCount == 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

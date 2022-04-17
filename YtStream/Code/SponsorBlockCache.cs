using System.Threading.Tasks;
using YtStream.Models;

namespace YtStream
{
    public static class SponsorBlockCache
    {
        /// <summary>
        /// Gets time ranges for the given youtube id and utilizes cache where possible.
        /// </summary>
        /// <param name="ytid">Youtube id</param>
        /// <param name="Settings">
        /// Settings. If null or not supplied, will pull config from file
        /// </param>
        /// <returns>Time ranges</returns>
        public static async Task<TimeRange[]> GetRangesAsync(string ytid, ConfigModel Settings = null)
        {
            if (Settings == null)
            {
                Settings = ConfigModel.Load();
            }
            var C = Cache.GetHandler(Cache.CacheType.SponsorBlock, Settings.CacheSBlockLifetime);
            var FN = Tools.GetIdName(ytid) + ".json";
            var FS = C.OpenIfNotStale(FN);
            if (FS != null)
            {
                var Json = await Tools.ReadStringAsync(FS);
                var Segments = Json.FromJson<TimeRange[]>();
                if (Segments != null)
                {
                    return Segments;
                }
                //Cache problem. Get live data
            }
            var Ranges = await SponsorBlock.GetRangesAsync(ytid);
            if (Ranges != null)
            {
                using (var Cache = C.WriteFile(FN))
                {
                    await Tools.WriteStringAsync(Cache, Ranges.ToJson());
                }
                return Ranges;
            }
            return new TimeRange[0];
        }
    }
}

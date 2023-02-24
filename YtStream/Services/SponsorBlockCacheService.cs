using AyrA.AutoDI;
using System;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Extensions;
using YtStream.Models;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Transient)]
    public class SponsorBlockCacheService
    {
        /// <summary>
        /// Default sponsorblock cache time
        /// </summary>
        public const int SponsorBlockCacheTime = 86400 * 7;

        private readonly CacheService _cache;
        private readonly SponsorBlockService _sb;
        private readonly ConfigModel _config;

        public SponsorBlockCacheService(CacheService cache, SponsorBlockService sb, ConfigService config)
        {
            _cache = cache;
            _sb = sb;
            _config = config.GetConfiguration();
        }

        /// <summary>
        /// Gets time ranges for the given youtube id and utilizes cache where possible.
        /// </summary>
        /// <param name="ytid">Youtube id</param>
        /// <param name="Settings">
        /// Settings. If null or not supplied, will pull config from file
        /// </param>
        /// <returns>Time ranges</returns>
        public async Task<TimeRangeModel[]> GetRangesAsync(string ytid)
        {
            var C = _cache.GetHandler(CacheTypeEnum.SponsorBlock, _config.CacheSBlockLifetime);
            var FN = Tools.GetIdName(ytid) + ".json";
            var FS = C.OpenIfNotStale(FN);
            if (FS != null)
            {
                var Json = await Tools.ReadStringAsync(FS);
                var Segments = Json.FromJson<TimeRangeModel[]>();
                if (Segments != null)
                {
                    return Segments;
                }
                //Cache problem. Get live data
            }
            var Ranges = await _sb.GetRangesAsync(ytid);
            if (Ranges != null)
            {
                using (var Cache = C.WriteFile(FN))
                {
                    await Tools.WriteStringAsync(Cache, Ranges.ToJson());
                }
                return Ranges;
            }
            return Array.Empty<TimeRangeModel>();
        }
    }
}

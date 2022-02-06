using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream.Controllers
{
    public class StreamController : Controller
    {
        private static string[] SplitIds(string id)
        {
            return string.IsNullOrEmpty(id) ? new string[0] : id.Split(',');
        }

        public IActionResult Order(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("No id specified");
            }
            var ids = SplitIds(id);
            if (ids.Length > 0)
            {
                var Response = string.Join("\r\n", ids.Select(m => Tools.IsYoutubeId(m) ? $"{m}: Valid" : $"{m}: Invalid"));
                return Ok(Response);
            }
            return BadRequest("No video id specified");
        }

        public async Task<IActionResult> Ranges(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("No id specified");
            }
            if (Tools.IsYoutubeId(id))
            {
                var Name = $"{Tools.GetIdName(id)}.json";
                var C = Cache.GetHandler(Cache.CacheType.SponsorBlock, Tools.SponsorBlockCacheTime);
                TimeRange[] Ranges;
                var FS = C.OpenIfNotStale(Name);

                if (FS == null)
                {
                    Ranges = await SponsorBlock.GetRangesAsync(id);
                    if (Ranges == null)
                    {
                        return StatusCode(502, "Got invalid SponsorBlock response");
                    }
                    using (FS = C.WriteFile(Name))
                    {
                        await Tools.WriteStringAsync(FS, Ranges.ToJson());
                    }
                    Response.Headers.Add("X-From-Cache", "No");
                }
                else
                {
                    using (FS)
                    {
                        Ranges = (await Tools.ReadStringAsync(FS)).FromJson<TimeRange[]>();
                    }
                    if (Ranges == null)
                    {
                        C.DeleteFile(Name);
                        return StatusCode(500, "Cache corrupt");
                    }
                    Response.Headers.Add("X-From-Cache", "Yes");
                }
                Tools.SetExpiration(Response, C.TimeToStale(Name));
                return Json(Ranges);
            }
            return BadRequest("Invalid id");
        }

        public IActionResult Random(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("No id specified");
            }
            var ids = SplitIds(id);
            if (ids.Length > 0)
            {
                Tools.Shuffle(ids, true);
                var Response = string.Join("\r\n", ids.Select(m => Tools.IsYoutubeId(m) ? $"{m}: Valid" : $"{m}: Invalid"));
                return Ok(Response);
            }
            return BadRequest("No video id specified");
        }
    }
}

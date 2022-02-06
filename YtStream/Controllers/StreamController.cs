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
                var Ranges = await SponsorBlock.GetRangesAsync(id);
                if (Ranges == null)
                {
                    return StatusCode(502, "Got invalid SponsorBlock response");
                }
                return Ok(string.Join("\r\n", Ranges.Select(m => m.ToString()).ToArray()));
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

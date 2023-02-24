using System.Collections.Generic;
using System.Linq;
using YtStream.Enums;

namespace YtStream.Models.Ad
{
    /// <summary>
    /// Represents Ad config
    /// </summary>
    public class AdConfigModel
    {
        /// <summary>
        /// Ads that play before the first stream
        /// </summary>
        public List<string> Intro { get; set; }
        /// <summary>
        /// Ads that play after the last stream
        /// </summary>
        public List<string> Outro { get; set; }
        /// <summary>
        /// Ads that play in between streams
        /// </summary>
        public List<string> Inter { get; set; }

        /// <summary>
        /// New instance
        /// </summary>
        public AdConfigModel()
        {
            Intro = new();
            Outro = new();
            Inter = new();
        }

        /// <summary>
        /// Adds an ad to the given types
        /// </summary>
        /// <param name="Name">File name</param>
        /// <param name="Type">Types</param>
        /// <returns>true, if added</returns>
        /// <remarks>Will not remove the ad from unspecified types</remarks>
        public bool Add(string Name, AdTypeEnum Type)
        {
            var ok = false;
            if (Type.HasFlag(AdTypeEnum.Inter) && !Inter.Contains(Name))
            {
                Inter.Add(Name);
                ok = true;
            }
            if (Type.HasFlag(AdTypeEnum.Intro) && !Intro.Contains(Name))
            {
                Intro.Add(Name);
                ok = true;
            }
            if (Type.HasFlag(AdTypeEnum.Outro) && !Outro.Contains(Name))
            {
                Outro.Add(Name);
                ok = true;
            }
            return ok;
        }

        /// <summary>
        /// Removes an add from all supplied categories
        /// </summary>
        /// <param name="Name">File name</param>
        /// <param name="Type">Types</param>
        /// <returns>True if removed</returns>
        /// <remarks>Ad file itself is not deleted</remarks>
        public bool Remove(string Name, AdTypeEnum Type)
        {
            var ok = false;
            if (Type.HasFlag(AdTypeEnum.Inter))
            {
                ok |= Inter.Remove(Name);
            }
            if (Type.HasFlag(AdTypeEnum.Intro))
            {
                ok |= Intro.Remove(Name);
            }
            if (Type.HasFlag(AdTypeEnum.Outro))
            {
                ok |= Outro.Remove(Name);
            }
            return ok;
        }

        /// <summary>
        /// Fix up the class after deserialization
        /// </summary>
        public void Fix()
        {
            if (Inter == null)
            {
                Inter = new List<string>();
            }
            else
            {
                Inter = Inter.Distinct().ToList();
            }

            if (Intro == null)
            {
                Intro = new List<string>();
            }
            else
            {
                Intro = Intro.Distinct().ToList();
            }

            if (Outro == null)
            {
                Outro = new List<string>();
            }
            else
            {
                Outro = Outro.Distinct().ToList();
            }
            Inter.Sort();
            Intro.Sort();
            Outro.Sort();
        }
    }
}

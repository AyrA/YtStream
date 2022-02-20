using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YtStream
{
    /// <summary>
    /// Generic functions
    /// </summary>
    public static class Tools
    {
        /// <summary>
        /// Default sponsorblock cache time
        /// </summary>
        public const int SponsorBlockCacheTime = 86400 * 7;

        /// <summary>
        /// HTTP date for zero
        /// </summary>
        private const string UnixZero = "Thu, 01 Jan 1970 00:00:00 GMT";
        /// <summary>
        /// Parse string for unix date
        /// </summary>
        public const string UnixZeroParse = "1970-00-00T00:00:00Z";

        /// <summary>
        /// Regex of a valid youtube video id
        /// </summary>
        /// <seealso cref="https://cable.ayra.ch/help/fs.php?help=youtube_id"/>
        private static readonly Regex IdRegex = new Regex(@"^[\w\-]{10}[AEIMQUYcgkosw048]$");
        /// <summary>
        /// Regex of a regular playlist
        /// </summary>
        private static readonly Regex PlRegex = new Regex(@"^PL(?:[\dA-F]{16}|[\w\-]{32})$");
        /// <summary>
        /// RNG for non cryptographic purposes
        /// </summary>
        private static readonly Random R = new Random();

        /// <summary>
        /// Get response from web request with exception handling
        /// </summary>
        /// <param name="req">request</param>
        /// <returns>response</returns>
        /// <remarks>
        /// This only handles a <see cref="WebException"/>
        /// </remarks>
        public async static Task<HttpWebResponse> GetResponseAsync(HttpWebRequest req)
        {
            try
            {
                return (HttpWebResponse)await req.GetResponseAsync();
            }
            catch (WebException ex)
            {
                return (HttpWebResponse)ex.Response;
            }
        }

        /// <summary>
        /// Shuffles an array in an unbiased way
        /// </summary>
        /// <typeparam name="T">Array type</typeparam>
        /// <param name="Array">Array to shuffle</param>
        /// <param name="InPlace">true, to shuffle the array in-place</param>
        /// <returns>
        /// Shuffled array. Is just a reference to <paramref name="Array"/>
        /// if in-place shuffling is selected, or shuffling not possible.
        /// </returns>
        /// <remarks>
        /// This is basically a Fisher–Yates shuffle.
        /// Not cryptographically safe
        /// </remarks>
        public static T[] Shuffle<T>(T[] Array, bool InPlace = false)
        {
            if (Array != null && Array.Length > 1)
            {
                //List of indexes
                var Indexes = Enumerable.Range(0, Array.Length).ToList();
                //Destination array
                T[] Temp = new T[Array.Length];
                for (var i = 0; i < Temp.Length; i++)
                {
                    if (Indexes.Count > 1)
                    {
                        //Pick a random index
                        var Index = R.Next(0, Indexes.Count);
                        //Assign value at that index to the next free slot in the destination array
                        Temp[i] = Array[Indexes[Index]];
                        //Remove this index to not pick it twice
                        Indexes.RemoveAt(Index);
                    }
                    else
                    {
                        //Last remaining item doesn't needs to be random picked
                        Temp[i] = Array[Indexes[0]];
                    }
                }
                //If in-place randomization is desired,
                //copy randomized array back
                if (InPlace)
                {
                    for (var i = 0; i < Temp.Length; i++)
                    {
                        Array[i] = Temp[i];
                    }
                    //Return reference to original array
                    return Array;
                }
                return Temp;
            }
            return Array;
        }

        /// <summary>
        /// Checks if the form checkbox confirmation created by "_FormSafetyCheck.cshtml" is correct.
        /// </summary>
        /// <param name="values">Form values</param>
        /// <remarks>Throws an exception on failure with the details</remarks>
        public static void CheckFormConfirmation(IFormCollection values)
        {
            var Check = values["FormSafetyCheck"];
            var Values = values["FormSafetyConfirm"];
            if (Check.Count != 1)
            {
                throw new ArgumentException("Invalid confirmation field value. Please reload the page. " +
                    "If the error persists, disable browser extensions that may interfere with form fields.");
            }
            if (Values.Count == 0)
            {
                throw new ArgumentException("No confirmation checkbox was selected");
            }
            if (Values.Count > 1)
            {
                throw new ArgumentException("More than one confirmation checkbox was selected");
            }
            if (Check.ToString() != Values.ToString())
            {
                throw new ArgumentException("The wrong confirmation checkbox was selected");
            }
        }

        /// <summary>
        /// Sets the "Expires" header to the given time
        /// </summary>
        /// <param name="response">HTTP handler</param>
        /// <param name="duration">Expiration timeout</param>
        /// <returns>
        /// Date the header was set to
        /// </returns>
        /// <remarks>A value in the past results in "1970-01-01 00:00:00 GMT"</remarks>
        public static DateTime SetExpiration(HttpResponse response, TimeSpan duration)
        {
            if (duration.Ticks <= 0)
            {
                response.Headers["Expires"] = UnixZero;
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            var EndDate = DateTime.UtcNow.Add(duration);
            response.Headers["Expires"] = EndDate.ToString("R");
            return EndDate;
        }

        /// <summary>
        /// Sets headers used to stream MP3 data to the client
        /// </summary>
        /// <param name="response">HTTP handler</param>
        /// <returns>true if headers were set</returns>
        public static bool SetAudioHeaders(HttpResponse response)
        {
            if (!response.HasStarted)
            {
                response.ContentType = "audio/mpeg";
                response.Headers.Add("transferMode.dlna.org", "Streaming");
                response.Headers.Add("contentFeatures.dlna.org", "DLNA.ORG_PN=MP3;DLNA.ORG_OP=01;DLNA.ORG_FLAGS=01700000000000000000000000000000");
            }
            return response.HasStarted;
        }

        /// <summary>
        /// Checks if the given id is a technically valid YT id
        /// </summary>
        /// <param name="Id">video id</param>
        /// <returns>true if valid</returns>
        /// <remarks>This will not contact youtube and only check formal correctness</remarks>
        public static bool IsYoutubeId(string Id)
        {
            return !string.IsNullOrEmpty(Id) &&
                IdRegex.IsMatch(Id);
        }

        /// <summary>
        /// Checks if the supplied argument is a potentially valid yt playlist id
        /// </summary>
        /// <param name="playlist">Playlist id</param>
        /// <returns>true, if valid playlist</returns>
        public static bool IsYoutubePlaylist(string playlist)
        {
            if (string.IsNullOrEmpty(playlist))
            {
                return false;
            }
            //There's way too many damn playlist formats around.
            //Lists I know of:
            //"FL" followed by hex or urlbase64: Favs
            //"OL" followed by hex or urlbase64: Saved list from someone else
            //"PL" followed by hex: Old playlist
            //"PL" followed by urlbase64: Newer playlist
            //"LL": Liked videos

            //Because this service acts anonymously, FL,OL,LL are not actually supported.
            return PlRegex.IsMatch(playlist);
        }

        /// <summary>
        /// Converts an Id into a standard youtube watch URL
        /// </summary>
        /// <param name="Id">video Id</param>
        /// <returns>Youtube URL</returns>
        public static string IdToUrl(string Id)
        {
            if (IsYoutubeId(Id))
            {
                return $"https://www.youtube.com/watch?v={Id}";
            }
            throw new ArgumentException("Invalid youtube id");
        }

        /// <summary>
        /// Gets a unique file name for the given id
        /// </summary>
        /// <param name="Id">video id</param>
        /// <returns>File name</returns>
        /// <remarks>
        /// This function is useful for case insensitive file systems
        /// </remarks>
        public static string GetIdName(string Id)
        {
            if (!IsYoutubeId(Id))
            {
                throw new FormatException("Argument must be a youtube id");
            }
            var Num = Convert.FromBase64String(Id.Replace('_', '/').Replace('-', '+') + "=");
            return string.Concat(Num.Select(m => m.ToString("X2")));
        }

        /// <summary>
        /// Reads a steam as an UTF-8 string
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>String</returns>
        public static string ReadString(Stream stream)
        {
            using (var SR = new StreamReader(stream, leaveOpen: true))
            {
                return SR.ReadToEnd();
            }
        }

        /// <summary>
        /// Reads a steam as an UTF-8 string
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>String</returns>
        public async static Task<string> ReadStringAsync(Stream stream)
        {
            using (var SR = new StreamReader(stream, leaveOpen: true))
            {
                return await SR.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Writes an UTF-8 encoded string to a stream
        /// </summary>
        /// <param name="S">Stream</param>
        /// <param name="Data">String</param>
        /// <remarks>Will not write BOM or a length prefix</remarks>
        public static void WriteString(Stream S, string Data)
        {
            if (!string.IsNullOrEmpty(Data))
            {
                var bytes = Encoding.UTF8.GetBytes(Data);
                S.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Writes an UTF-8 encoded string to a stream
        /// </summary>
        /// <param name="S">Stream</param>
        /// <param name="Data">String</param>
        /// <remarks>Will not write BOM or a length prefix</remarks>
        public async static Task WriteStringAsync(Stream S, string Data)
        {
            if (!string.IsNullOrEmpty(Data))
            {
                var bytes = Encoding.UTF8.GetBytes(Data);
                await S.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Gets Enums in the form "Name1234" as select item list that displays "1234 Name"
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>Enum list for "asp-items" entry</returns>
        public static IEnumerable<SelectListItem> HtmlEnumSwapList<T>() where T : Enum
        {
            var Values = Enum.GetValues(typeof(T)).OfType<T>();
            foreach (var V in Values)
            {
                yield return new SelectListItem(SwapEnumName(V), V.ToString());
            }
        }

        /// <summary>
        /// Swaps "NameValue" enum string into "Value Name"
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="EnumValue">Enum value</param>
        /// <returns>
        /// "Value Name" format. Returns name as-is if it doesn't matches "NameValue"
        /// </returns>
        public static string SwapEnumName<T>(T EnumValue) where T : Enum
        {
            var Lbl = Regex.Match(EnumValue.ToString(), @"([a-zA-Z]+)(\d+)");
            if (Lbl.Success)
            {
                return Lbl.Groups[2].Value + " " + Lbl.Groups[1].Value;
            }
            return EnumValue.ToString();
        }

        /// <summary>
        /// Checks if a combined enum only consists of existing flags
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="EnumValue">Enum value</param>
        /// <returns></returns>
        public static bool CheckEnumFlags<T>(T EnumValue) where T : Enum
        {
            //Don't bother with more complex checks if it's a defined value
            if (Enum.IsDefined(EnumValue.GetType(), EnumValue))
            {
                return true;
            }
            ulong Flags = 0;
            ulong Supplied = Convert.ToUInt64(EnumValue);
            foreach (var Value in Enum.GetValues(EnumValue.GetType()).OfType<T>())
            {
                Flags |= Convert.ToUInt64(Value);
            }
            return Flags == (Supplied | Flags);
        }

        /// <summary>
        /// Converts a size in bytes into nicer display format for people
        /// </summary>
        /// <param name="d">Size in bytes</param>
        /// <returns>
        /// Nice size on success. "0 Bytes" if negative, "unknown" if NaN or infinite</returns>
        public static string NiceSize(double d)
        {
            int index = 0;
            var sizes = "Bytes,KB,MB,GB,TB,PB,EX,ZB,YB".Split(',');
            if (!double.IsFinite(d)) //Handles NaN too
            {
                return "unknown";
            }
            while (d >= 1024.0 && index < sizes.Length - 1)
            {
                ++index;
                d /= 1024.0;
            }
            return $"{Math.Round(Math.Max(0.0, d), 2)} {sizes[index]}";
        }

        /// <summary>
        /// Gets a random number from the unsafe RNG
        /// </summary>
        /// <param name="MinIncl">Minimum number (included)</param>
        /// <param name="MaxExcl">Maximum number (excluded)</param>
        /// <returns>X where <paramref name="MinIncl"/> &lt;= X &lt; <paramref name="MaxExcl"/></returns>
        public static int GetRandom(int MinIncl, int MaxExcl)
        {
            lock (R)
            {
                return R.Next(MinIncl, MaxExcl);
            }
        }

        /// <summary>
        /// Parse the antiforgery token into a C# object
        /// </summary>
        /// <param name="FormElement">Antiforgery element. Usually <see cref="IHtmlHelper.AntiForgeryToken()"/></param>
        /// <returns>Object with field name and value</returns>
        public static Antiforgery ParseAntiforgery(Microsoft.AspNetCore.Html.IHtmlContent FormElement)
        {
            var Pattern = new Regex("(\\w+)\\s*=\\s*\"([^\"]+)\"");
            string Form;
            using (var SW = new StringWriter())
            {
                FormElement.WriteTo(SW, System.Text.Encodings.Web.HtmlEncoder.Default);
                Form = SW.ToString();
            }
            var MM = Pattern.Matches(Form).OfType<Match>().ToArray();
            var AF = new Antiforgery();
            foreach (var M in MM)
            {
                if (M.Groups[1].Value.ToLower() == "name")
                {
                    AF.Name = M.Groups[2].Value;
                }
                if (M.Groups[1].Value.ToLower() == "value")
                {
                    AF.Value = M.Groups[2].Value;
                }
            }
            return AF;
        }

        public struct Antiforgery
        {
            public string Name, Value;
        }
    }
}

﻿using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
        /// Regex of a valid youtube video id
        /// </summary>
        /// <seealso cref="https://cable.ayra.ch/help/fs.php?help=youtube_id"/>
        private static readonly Regex IdRegex = new Regex(@"^[\w\-]{10}[AEIMQUYcgkosw048]=?$");
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
    }
}

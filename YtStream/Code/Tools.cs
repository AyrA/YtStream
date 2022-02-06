using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YtStream
{
    public static class Tools
    {
        private static readonly Regex IdRegex = new Regex(@"^[\w\-]{10}[AEIMQUYcgkosw048]=?$");
        private static readonly Random R = new Random();
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

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
            catch(WebException ex)
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

        public static bool IsYoutubeId(string Id)
        {
            return !string.IsNullOrEmpty(Id) &&
                IdRegex.IsMatch(Id);
        }

        public static string ReadString(Stream stream)
        {
            using (var SR = new StreamReader(stream))
            {
                return SR.ReadToEnd();
            }
        }

        public async static Task<string> ReadStringAsync(Stream stream)
        {
            using (var SR = new StreamReader(stream))
            {
                return await SR.ReadToEndAsync();
            }
        }

        public static bool IsValidFileName(string Name)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return false;
            }
            return !Name.Any(m => InvalidNameChars.Contains(m));
        }
    }
}

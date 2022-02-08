using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.MP3;

namespace YtStream
{
    /// <summary>
    /// Cuts and filters MP3 data
    /// </summary>
    public static class Mp3Cut
    {
        /// <summary>
        /// Cuts an MP3 file according to ranges.
        /// Also removes any non-audio bytes.
        /// </summary>
        /// <param name="Ranges">
        /// Rages to cut.
        /// Can be null or an empty enumeration to filter for audio only
        /// </param>
        /// <param name="Source">
        /// Source stream with uncut, unfiltered MP3 data
        /// </param>
        /// <param name="Target">
        /// Target stream for filtered and cut MP3 data
        /// </param>
        /// <param name="UncutOutput">
        /// Target stream for filtered only data.
        /// Can be null if not in use.
        /// </param>
        public static void CutMp3(IEnumerable<TimeRange> Ranges, Stream Source, Stream Target, Stream UncutOutput = null)
        {
            Task.WaitAll(CutMp3Async(Ranges, Source, Target, UncutOutput));
        }

        /// <summary>
        /// Cuts an MP3 file according to ranges.
        /// Also removes any non-audio bytes.
        /// </summary>
        /// <param name="Ranges">
        /// Rages to cut.
        /// Can be null or an empty enumeration to filter for audio only
        /// </param>
        /// <param name="Source">
        /// Source stream with uncut, unfiltered MP3 data
        /// </param>
        /// <param name="Target">
        /// Target stream for filtered and cut MP3 data
        /// </param>
        /// <param name="UncutOutput">
        /// Target stream for filtered only data.
        /// Can be null if not in use.
        /// </param>
        public static async Task CutMp3Async(IEnumerable<TimeRange> Ranges, Stream Source, Stream Target, Stream UncutOutput = null)
        {
            TimeRange[] R;
            double TimeMS = 0.0;
            //If null, the caller wants to filter invalid data only
            if (Ranges == null)
            {
                R = new TimeRange[0];
            }
            else
            {
                R = Ranges.Where(m => m.IsValid).OrderBy(m => m.Start).ToArray();
            }
            if (UncutOutput != null && !UncutOutput.CanWrite)
            {
                throw new ArgumentException($"If supplied, {nameof(UncutOutput)} must be writable");
            }

            if (Target == null)
            {
                throw new ArgumentNullException(nameof(Target));
            }

            byte[] Header = new byte[4];
            MP3Header Parsed;
            if (!await GuaranteedRead(Header, 0, Header.Length, Source))
            {
                //Stream not long enough for initial header material
                return;
            }
            while (true)
            {
                Parsed = null;
                if (MP3Header.IsHeader(Header))
                {
                    try
                    {
                        Parsed = new MP3Header(Header);
                    }
                    catch
                    {
                        //NOOP
                    }
                }
                if (Parsed == null)
                {
                    //On invalid header: go bytewise to the next until partial sync is detected
                    do
                    {
                        int Next = Source.ReadByte();
                        if (Next < 0) //EOF
                        {
                            //Stream end within invalid data
                            return;
                        }
                        //If you want to output invalid bytes, send Header[0] to the target stream here

                        Header[0] = Header[1];
                        Header[1] = Header[2];
                        Header[2] = Header[3];
                        Header[3] = (byte)Next;
                    } while (Header[0] != 0xFF); //This is faster and simpler than the IsHeader check
                    continue;
                }
                else
                {
                    //Valid header at this point. Read audio portion
                    byte[] Audio = new byte[Parsed.NumberOfBytes];
                    if (!await GuaranteedRead(Audio, 0, Audio.Length, Source))
                    {
                        //Stream too short for audio data
                        return;
                    }
                    //Decide whether to output or discard it
                    if (!R.Any(m => m.IsInRange(TimeMS / 1000.0)))
                    {
                        await Target.WriteAsync(Header, 0, Header.Length);
                        await Target.WriteAsync(Audio, 0, Audio.Length);
                    }
                    //Always output to filter only stream
                    if (UncutOutput != null)
                    {
                        await UncutOutput.WriteAsync(Header, 0, Header.Length);
                        await UncutOutput.WriteAsync(Audio, 0, Audio.Length);
                    }
                    TimeMS += Parsed.AudioLengthMS;
                    //Read next header
                    if (!await GuaranteedRead(Header, 0, Header.Length, Source))
                    {
                        //Stream too short for next header
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Reads exactly as many bytes as requested.
        /// Useful for streams that may fill slower than we try to read from them
        /// </summary>
        /// <param name="buffer">Buffer to fill</param>
        /// <param name="offset">Offset to start filling from</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="source">Stream to read from</param>
        /// <returns>true if sucessfully read, false if EOF</returns>
        private async static Task<bool> GuaranteedRead(byte[] buffer, int offset, int count, Stream source)
        {
            int total = 0;
            while (total < count)
            {
                int read = await source.ReadAsync(buffer, offset + total, count - total);
                //EOF
                if (read == 0)
                {
                    return false;
                }
                total += read;
            }
            return true;
        }
    }
}

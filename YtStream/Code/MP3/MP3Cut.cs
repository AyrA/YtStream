using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream.MP3
{
    /// <summary>
    /// Cuts and filters MP3 data
    /// </summary>
    public static class MP3Cut
    {
        /// <summary>
        /// Filters invalid MP3 data and writes clean MP3 to the destination
        /// </summary>
        /// <param name="Source">Source stream</param>
        /// <param name="Dest">Target stream</param>
        public static void FilterMp3(Stream Source, Stream Dest)
        {
            var Conf = new MP3CutTargetStreamConfig();
            Conf.AddStream(new MP3CutTargetStreamInfo(Dest, true, false, false));
            CutMp3(null, Source, Conf);
        }

        /// <summary>
        /// Filters invalid MP3 data and writes clean MP3 to the destination
        /// </summary>
        /// <param name="Source">Source stream</param>
        /// <param name="Dest">Target stream</param>
        public static Task FilterMp3Async(Stream Source, Stream Dest)
        {
            var Conf = new MP3CutTargetStreamConfig();
            Conf.AddStream(new MP3CutTargetStreamInfo(Dest, true, false, false));
            return CutMp3Async(null, Source, Conf);
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
        /// <param name="PreventStalling">
        /// Aborts conversion if set to true and speed falls below 1x playback speed
        /// </param>
        public static void CutMp3(IEnumerable<TimeRange> Ranges, Stream Source, MP3CutTargetStreamConfig Output)
        {
            Task.WaitAll(CutMp3Async(Ranges, Source, Output));
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
        /// <param name="PreventStalling">
        /// Aborts conversion if set to true and speed falls below 1x playback speed
        /// </param>
        public static async Task CutMp3Async(IEnumerable<TimeRange> Ranges, Stream Source, MP3CutTargetStreamConfig Output)
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
            if (Output == null || Output.Streams.Length == 0)
            {
                throw new ArgumentNullException(nameof(Output));
            }

            byte[] Header = new byte[4];
            MP3Header Parsed;
            if (!await GuaranteedRead(Header, 0, Header.Length, Source))
            {
                //Stream not long enough for initial header material
                return;
            }
            var SW = Stopwatch.StartNew();
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
                    var Skip = R.Any(m => m.IsInRange(TimeMS / 1000.0));
                    var Streams = Output.Streams.Where(m => !m.Faulted).ToArray();
                    //No more streams remaining to write to
                    if (Streams.Length == 0)
                    {
                        return;
                    }
                    foreach (var Info in Streams)
                    {
                        if (Info.IsUncut || !Skip)
                        {
                            //Clear "private" bit
                            Header[2] &= 0xFE;
                            try
                            {
                                await Info.Stream.WriteAsync(Header, 0, Header.Length);
                                await Info.Stream.WriteAsync(Audio, 0, Audio.Length);
                            }
                            catch
                            {
                                Info.SetFaulted(true);
                            }
                        }
                    }

                    if (SW.IsRunning && TimeMS > 1000.0 && TimeMS < SW.ElapsedMilliseconds)
                    {
                        Output.SetTimeout(true);
                        SW.Stop();
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
        /// Reads <paramref name="Source"/> as MP3
        /// and sends it to <paramref name="Output"/>.
        /// Sets or clears the "private" bit according to <paramref name="Mark"/>
        /// </summary>
        /// <param name="Source">Source MP3 stream</param>
        /// <param name="Output">Target MP3 stream</param>
        /// <param name="Mark">Set or clear private bit</param>
        /// <returns>true if at least one audio chunk was sent, false otherwise</returns>
        public static async Task<bool> SendAd(Stream Source, Stream Output, bool Mark)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }
            if (Output == null)
            {
                throw new ArgumentNullException(nameof(Output));
            }
            bool sent = false;
            byte[] Header = new byte[4];
            MP3Header Parsed;
            if (!await GuaranteedRead(Header, 0, Header.Length, Source))
            {
                //Stream not long enough for initial header material
                return false;
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
                            return sent;
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
                        return sent;
                    }
                    //Clear or set "private" bit according to "Mark"
                    if (Mark)
                    {
                        Header[2] |= 1;
                    }
                    else
                    {
                        Header[2] &= 0xFE;
                    }
                    try
                    {
                        await Output.WriteAsync(Header, 0, Header.Length);
                        await Output.WriteAsync(Audio, 0, Audio.Length);
                        sent = true;
                    }
                    catch
                    {
                        return sent;
                    }
                    //Read next header
                    if (!await GuaranteedRead(Header, 0, Header.Length, Source))
                    {
                        //Stream too short for next header
                        return sent;
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

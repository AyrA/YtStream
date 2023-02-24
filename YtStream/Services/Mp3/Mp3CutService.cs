using AyrA.AutoDI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Models;
using YtStream.Models.Mp3;

namespace YtStream.Services.Mp3
{
    [AutoDIRegister(AutoDIType.Transient)]
    /// <summary>
    /// Cuts and filters MP3 data
    /// </summary>
    public class Mp3CutService
    {
        /// <summary>
        /// Filters invalid MP3 data and writes clean MP3 to the destination
        /// </summary>
        /// <param name="Source">Source stream</param>
        /// <param name="Dest">Target stream</param>
        public void FilterMp3(Stream Source, Stream Dest)
        {
            var Conf = new Mp3CutTargetStreamConfigModel();
            Conf.AddStream(new Mp3CutTargetStreamInfoModel(Dest, true, false, false, false));
            CutMp3(null, Source, Conf);
        }

        /// <summary>
        /// Filters invalid MP3 data and writes clean MP3 to the destination
        /// </summary>
        /// <param name="Source">Source stream</param>
        /// <param name="Dest">Target stream</param>
        public Task FilterMp3Async(Stream Source, Stream Dest)
        {
            var Conf = new Mp3CutTargetStreamConfigModel();
            Conf.AddStream(new Mp3CutTargetStreamInfoModel(Dest, true, false, false, false));
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
        public void CutMp3(IEnumerable<TimeRangeModel> Ranges, Stream Source, Mp3CutTargetStreamConfigModel Output)
        {
            CutMp3Async(Ranges, Source, Output).Wait();
        }

        /// <summary>
        /// Cuts an MP3 file according to ranges.
        /// Also removes any non-audio bytes.
        /// </summary>
        /// <param name="ranges">
        /// Rages to cut.
        /// Can be null or an empty enumeration to filter for audio only
        /// </param>
        /// <param name="source">
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
        public async Task CutMp3Async(IEnumerable<TimeRangeModel> ranges, Stream source, Mp3CutTargetStreamConfigModel output)
        {
            TimeRangeModel[] R;
            //Total audio time of the enire MP3 that has been processed
            double TotalTimeMS = 0.0;
            //Audio time of the cut MP3 that has been processed
            double CutTimeMS = 0.0;
            Stopwatch GlobalTimer = Stopwatch.StartNew();
            //If null, the caller wants to filter invalid data only
            if (ranges == null)
            {
                R = Array.Empty<TimeRangeModel>();
            }
            else
            {
                R = ranges.Where(m => m.IsValid).OrderBy(m => m.Start).ToArray();
            }
            if (output == null || output.Streams.Length == 0)
            {
                throw new ArgumentNullException(nameof(output));
            }

            byte[] Header = new byte[4];
            Mp3HeaderModel Parsed;
            if (!await GuaranteedRead(Header, 0, Header.Length, source))
            {
                //Stream not long enough for initial header material
                return;
            }
            var SW = Stopwatch.StartNew();
            while (true)
            {
                Parsed = null;
                if (Mp3HeaderModel.IsHeader(Header))
                {
                    try
                    {
                        Parsed = new Mp3HeaderModel(Header);
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
                        int Next = source.ReadByte();
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
                    if (!await GuaranteedRead(Audio, 0, Audio.Length, source))
                    {
                        //Stream too short for audio data
                        return;
                    }
                    var Skip = R.Any(m => m.IsInRange(TotalTimeMS / 1000.0));
                    var Streams = output.Streams.Where(m => !m.Faulted).ToArray();
                    //No more streams remaining to write to
                    if (Streams.Length == 0)
                    {
                        return;
                    }
                    if (!Skip)
                    {
                        CutTimeMS += Parsed.AudioLengthMS;
                    }
                    foreach (var Info in Streams)
                    {
                        if (Info.IsUncut || !Skip)
                        {
                            //Clear "private" bit
                            Header[2] &= 0xFE;
                            try
                            {
                                await Info.Stream.WriteAsync(Header);
                                await Info.Stream.WriteAsync(Audio);
                            }
                            catch
                            {
                                Info.SetFaulted(true);
                            }
                        }
                    }

                    if (SW.IsRunning && TotalTimeMS > 1000.0 && TotalTimeMS < SW.ElapsedMilliseconds)
                    {
                        output.SetTimeout(true);
                        SW.Stop();
                    }
                    TotalTimeMS += Parsed.AudioLengthMS;
                    //Read next header
                    if (!await GuaranteedRead(Header, 0, Header.Length, source))
                    {
                        //Stream too short for next header
                        return;
                    }
                    //Delay only if a functioning live stream remains
                    if (Streams.Any(m => m.LiveStream && !m.Faulted))
                    {
                        //Permit a 3 second buffer
                        while (CutTimeMS > GlobalTimer.ElapsedMilliseconds + 3000)
                        {
                            await Task.Delay(200);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads <paramref name="source"/> as MP3
        /// and sends it to <paramref name="output"/>.
        /// Sets or clears the "private" bit according to <paramref name="privateBit"/>
        /// </summary>
        /// <param name="source">Source MP3 stream</param>
        /// <param name="output">Target MP3 stream</param>
        /// <param name="privateBit">Set or clear private bit</param>
        /// <returns>true if at least one audio chunk was sent, false otherwise</returns>
        public async Task<bool> SendAd(Stream source, Stream output, bool privateBit)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            bool sent = false;
            byte[] Header = new byte[4];
            Mp3HeaderModel Parsed;
            if (!await GuaranteedRead(Header, 0, Header.Length, source))
            {
                //Stream not long enough for initial header material
                return false;
            }
            while (true)
            {
                Parsed = null;
                if (Mp3HeaderModel.IsHeader(Header))
                {
                    try
                    {
                        Parsed = new Mp3HeaderModel(Header);
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
                        int Next = source.ReadByte();
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
                    if (!await GuaranteedRead(Audio, 0, Audio.Length, source))
                    {
                        //Stream too short for audio data
                        return sent;
                    }
                    //Clear or set "private" bit according to "Mark"
                    if (privateBit)
                    {
                        Header[2] |= 1;
                    }
                    else
                    {
                        Header[2] &= 0xFE;
                    }
                    try
                    {
                        await output.WriteAsync(Header);
                        await output.WriteAsync(Audio);
                        sent = true;
                    }
                    catch
                    {
                        return sent;
                    }
                    //Read next header
                    if (!await GuaranteedRead(Header, 0, Header.Length, source))
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
                int read = await source.ReadAsync(buffer.AsMemory(offset + total, count - total));
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

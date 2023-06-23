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
    /// <summary>
    /// Cuts and filters MP3 data
    /// </summary>
    [AutoDIRegister(AutoDIType.Transient)]
    public class Mp3CutService
    {
        /// <summary>
        /// Filters invalid MP3 data and writes clean MP3 to the destination
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <param name="dest">Target stream</param>
        public Task FilterMp3Async(Stream source, Stream dest)
        {
            var Conf = new Mp3CutTargetStreamConfigModel();
            Conf.AddStream(new Mp3CutTargetStreamInfoModel(dest, true, false, false, false));
            return CutMp3Async(null, source, Conf, 0);
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
        /// <param name="output">
        /// Target stream configuration for processed MP3 data
        /// </param>
        /// <param name="timeBuffer">
        /// Number of seconds to send in advance
        /// </param>
        public void CutMp3(IEnumerable<TimeRangeModel>? ranges, Stream source, Mp3CutTargetStreamConfigModel output, int timeBuffer)
        {
            CutMp3Async(ranges, source, output, timeBuffer).Wait();
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
        /// <param name="output">
        /// Target stream configuration for processed MP3 data
        /// </param>
        /// <param name="timeBuffer">
        /// Number of seconds to send in advance
        /// </param>
        public async Task CutMp3Async(IEnumerable<TimeRangeModel>? ranges, Stream source, Mp3CutTargetStreamConfigModel output, int timeBuffer)
        {
            timeBuffer = Math.Max(1, timeBuffer) * 1000;
            TimeRangeModel[] R;
            //Total audio time of the enire MP3 that has been processed
            double totalTimeMS = 0.0;
            //Audio time of the cut MP3 that has been processed
            double cutTimeMS = 0.0;
            Stopwatch globalTimer = Stopwatch.StartNew();
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

            byte[] header = new byte[4];
            Mp3HeaderModel? parsed;
            if (!await GuaranteedRead(header, 0, header.Length, source))
            {
                //Stream not long enough for initial header material
                return;
            }
            var SW = Stopwatch.StartNew();
            while (true)
            {
                parsed = null;
                if (Mp3HeaderModel.IsHeader(header))
                {
                    try
                    {
                        parsed = new Mp3HeaderModel(header);
                    }
                    catch
                    {
                        //NOOP
                    }
                }
                if (parsed == null)
                {
                    //On invalid header: go bytewise to the next until partial sync is detected
                    do
                    {
                        int next = source.ReadByte();
                        if (next < 0) //EOF
                        {
                            //Stream end within invalid data
                            return;
                        }
                        //If you want to output invalid bytes, send Header[0] to the target stream here

                        header[0] = header[1];
                        header[1] = header[2];
                        header[2] = header[3];
                        header[3] = (byte)next;
                    } while (header[0] != 0xFF); //This is faster and simpler than the IsHeader check
                    continue;
                }
                else
                {
                    //Valid header at this point. Read audio portion
                    byte[] audio = new byte[parsed.NumberOfBytes];
                    if (!await GuaranteedRead(audio, 0, audio.Length, source))
                    {
                        //Stream too short for audio data
                        return;
                    }
                    var skip = R.Any(m => m.IsInRange(totalTimeMS / 1000.0));
                    var streams = output.Streams.Where(m => !m.Faulted).ToArray();
                    //No more streams remaining to write to
                    if (streams.Length == 0)
                    {
                        return;
                    }
                    if (!skip)
                    {
                        cutTimeMS += parsed.AudioLengthMS;
                    }
                    foreach (var info in streams)
                    {
                        if (info.IsUncut || !skip)
                        {
                            //Clear "private" bit
                            header[2] &= 0xFE;
                            try
                            {
                                await info.Stream.WriteAsync(header);
                                await info.Stream.WriteAsync(audio);
                            }
                            catch
                            {
                                info.SetFaulted(true);
                            }
                        }
                    }

                    if (SW.IsRunning && totalTimeMS > 1000.0 && totalTimeMS < SW.ElapsedMilliseconds)
                    {
                        output.SetTimeout(true);
                        SW.Stop();
                    }
                    totalTimeMS += parsed.AudioLengthMS;
                    //Read next header
                    if (!await GuaranteedRead(header, 0, header.Length, source))
                    {
                        //Stream too short for next header
                        return;
                    }
                    //Delay only if a functioning live stream remains
                    if (streams.Any(m => m.LiveStream && !m.Faulted))
                    {
                        //Permit a buffer to start live streams faster
                        while (cutTimeMS > globalTimer.ElapsedMilliseconds + timeBuffer)
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
        /// <returns>Number of milliseconds of audio that was sent</returns>
        public async Task<double> SendAd(Stream source, Stream output, bool privateBit)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            if (source == Stream.Null)
            {
                return 0.0;
            }
            double sentMS = 0.0;
            byte[] header = new byte[4];
            Mp3HeaderModel? parsed;
            if (!await GuaranteedRead(header, 0, header.Length, source))
            {
                //Stream not long enough for initial header material
                return sentMS;
            }
            while (true)
            {
                parsed = null;
                if (Mp3HeaderModel.IsHeader(header))
                {
                    try
                    {
                        parsed = new Mp3HeaderModel(header);
                    }
                    catch
                    {
                        //NOOP
                    }
                }
                if (parsed == null)
                {
                    //On invalid header: go bytewise to the next until partial sync is detected
                    do
                    {
                        int next = source.ReadByte();
                        if (next < 0) //EOF
                        {
                            //Stream end within invalid data
                            return sentMS;
                        }
                        //If you want to output invalid bytes, send Header[0] to the target stream here

                        header[0] = header[1];
                        header[1] = header[2];
                        header[2] = header[3];
                        header[3] = (byte)next;
                    } while (header[0] != 0xFF); //This is faster and simpler than the IsHeader check
                    continue;
                }
                else
                {
                    //Valid header at this point. Read audio portion
                    byte[] audio = new byte[parsed.NumberOfBytes];
                    if (!await GuaranteedRead(audio, 0, audio.Length, source))
                    {
                        //Stream too short for audio data
                        return sentMS;
                    }
                    //Clear or set "private" bit according to "Mark"
                    if (privateBit)
                    {
                        header[2] |= 1;
                    }
                    else
                    {
                        header[2] &= 0xFE;
                    }
                    try
                    {
                        await output.WriteAsync(header);
                        await output.WriteAsync(audio);

                        sentMS += parsed.AudioLengthMS;
                    }
                    catch
                    {
                        return sentMS;
                    }
                    //Read next header
                    if (!await GuaranteedRead(header, 0, header.Length, source))
                    {
                        //Stream too short for next header
                        return sentMS;
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

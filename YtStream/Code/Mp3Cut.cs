using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.MP3;

namespace YtStream
{
    public static class Mp3Cut
    {
        public static void CutMp3(IEnumerable<TimeRange> Ranges, Stream Source, Stream Target)
        {
            Task.WaitAll(CutMp3Async(Ranges, Source, Target));
        }

        public static async Task CutMp3Async(IEnumerable<TimeRange> Ranges, Stream Source, Stream Target)
        {
            TimeRange[] R;
            double TimeMS = 0.0;
            if (Ranges == null)
            {
                R = new TimeRange[0];
            }
            else
            {
                R = Ranges.Where(m => m.IsValid).OrderBy(m => m.Start).ToArray();
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

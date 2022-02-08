using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.MP3;

namespace YtStream
{
    public class Converter : IDisposable
    {
        public class AsyncStreamResult
        {
            public Task CopyStreamResult { get; }
            public Stream StandardOutputStream { get; }

            public AsyncStreamResult(Task CopyStreamResult, Stream StandardOutputStream)
            {
                this.CopyStreamResult = CopyStreamResult;
                this.StandardOutputStream = StandardOutputStream;
            }
        }

        private readonly string executable;
        private Process P;

        public Bitrate AudioRate { get; set; } = Bitrate.kbps192;

        public Frequency AudioFrequency { get; set; } = Frequency.Hz44100;

        public bool IsConverting { get => P != null && !P.HasExited; }

        public Converter(string Executable)
        {
            if (string.IsNullOrWhiteSpace(Executable))
            {
                throw new ArgumentException($"'{nameof(Executable)}' cannot be null or whitespace.", nameof(Executable));
            }
            if (!File.Exists(Executable))
            {
                throw new IOException("File not found");
            }
            executable = Executable;
        }

        public bool Abort()
        {
            try
            {
                using (P)
                {
                    P.Kill();
                }
                P = null;
            }
            catch
            {
                //NOOP
                return false;
            }
            return true;
        }

        public bool WaitForExit(int MaxWaitMs = int.MaxValue)
        {
            if (IsConverting)
            {
                return P.WaitForExit(MaxWaitMs);
            }
            return true;
        }

        public Task<bool> WaitForExitAsync(int MaxWaitMs = int.MaxValue)
        {
            if (IsConverting)
            {
                return Task.Run(delegate { return P.WaitForExit(MaxWaitMs); });
            }
            return Task.FromResult(true);
        }

        public Stream ConvertToMp3(string SourceFileOrUrl)
        {
            if (P != null)
            {
                throw new InvalidOperationException("Conversion already running");
            }
            var Args = GetArgs(SourceFileOrUrl);
            var PSI = new ProcessStartInfo(executable, Args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            P = Process.Start(PSI);
            P.Exited += ExitHandler;
            P.EnableRaisingEvents = true;
            return P.StandardOutput.BaseStream;
        }

        private void ExitHandler(object sender, EventArgs e)
        {
            var Temp = (Process)sender;
            try
            {
                Temp.Dispose();
            }
            catch
            {
                //NOOP
            }
            P = null;
        }

        public AsyncStreamResult ConvertToMp3(Stream SourceData)
        {
            if (P != null)
            {
                throw new InvalidOperationException("Conversion already running");
            }
            var Args = GetArgs(SourceData);
            var PSI = new ProcessStartInfo(executable, Args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };
            P = Process.Start(PSI);
            P.Exited += ExitHandler;
            P.EnableRaisingEvents = true;
            return new AsyncStreamResult(
                SourceData.CopyToAsync(P.StandardInput.BaseStream),
                P.StandardOutput.BaseStream);
        }

        public void Dispose()
        {
            var Temp = P;
            if (Temp != null)
            {
                try
                {
                    Temp.Kill();
                }
                catch
                {
                    //NOOP
                }
                try
                {
                    Temp.Dispose();
                }
                catch
                {
                    //NOOP
                }
                P = null;
            }
        }

        private string GetArgs(Stream Arg)
        {
            return $"-i pipe:0 " + GetBaseArg();
        }

        private string GetArgs(string Arg)
        {
            return $"-i \"{Arg}\" " + GetBaseArg();
        }

        private string GetBaseArg()
        {
            return $"-ab {(int)AudioRate}k -vn -ar {(int)AudioFrequency} -acodec mp3 -f mp3 -y pipe:1";
        }
    }
}

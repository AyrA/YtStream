using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YtStream
{
    public class YoutubeDl
    {
        private readonly string executable;

        public YoutubeDl(string Executable)
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

        public async Task<YoutubeDlResult> GetAudioDetails(string Id)
        {
            if (!Tools.IsYoutubeId(Id))
            {
                throw new FormatException("Argument must be a youtube video id");
            }
            var PSI = new ProcessStartInfo(executable, $"--skip-download --dump-json --format bestaudio {Tools.IdToUrl(Id)}");
            PSI.UseShellExecute = false;
            PSI.RedirectStandardOutput = true;
            using (var P = Process.Start(PSI))
            {
                return (await P.StandardOutput.ReadToEndAsync()).FromJson<YoutubeDlResult>();
            }
        }

        public async Task<string> GetAudioUrl(string Id)
        {
            return (await GetAudioDetails(Id)).Url;
        }
    }
}

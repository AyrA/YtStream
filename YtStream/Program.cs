using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;

namespace YtStream
{
    /// <summary>
    /// Main Entry point
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
#if DEBUG
            Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", "");
#endif
            //Set current directory because for services it's wrong.
            using (var P = Process.GetCurrentProcess())
            {
                var BaseDir = Path.GetDirectoryName(P.MainModule.FileName);
                while (!Directory.Exists(Path.Combine(BaseDir, "wwwroot")))
                {
                    var NewDir = Path.GetFullPath(Path.Combine(BaseDir, ".."));
                    if (NewDir == BaseDir)
                    {
                        throw new Exception("Unable to find path to 'wwwroot' folder");
                    }
                    BaseDir = NewDir;
                }
                Environment.CurrentDirectory = BaseDir;
            }
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Cnstruct hosting environment
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Hosting environment</returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

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
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Cnstruct hosting environment
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Hosting environment</returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}

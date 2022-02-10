using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YtStream.Models;

namespace YtStream
{
    /// <summary>
    /// Runs kerstel
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Base path of the application
        /// </summary>
        public static string BasePath { get; private set; }

        /// <summary>
        /// If set to true, prevents access to streaming functionality
        /// </summary>
        public static volatile bool Locked;

        /// <summary>
        /// Creates this instance
        /// </summary>
        /// <param name="configuration">Base configuration</param>
        /// <param name="env">Base environment</param>
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            ConfigModel C;
            Locked = false;
            Configuration = configuration;
            BasePath = env.ContentRootPath;
            try
            {
                C = ConfigModel.Load();
            }
            catch
            {
                C = null;
            }
            if (C == null)
            {
                Locked = true;
            }
            else if (C.UseCache)
            {
                Cache.SetBaseDirectory(C.CachePath);
            }
        }

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime.
        /// Use this method to add services to the container.
        /// </summary>
        /// <param name="services">Service collection</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
            });
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
            services.AddControllersWithViews();
            services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            {
                //Required for the BufferedStream to dispose correctly.
                //No writes will actualy be performed because we always call FlushAsync() before disposing.
                options.AllowSynchronousIO = true;
            });
        }

        /// <summary>
        /// This method gets called by the runtime.
        /// Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">Application builder</param>
        /// <param name="env">Hosting environment</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //Detailed errors for devs
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            //Deliver content from "wwwroot" folder
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            //Default MVC route
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}

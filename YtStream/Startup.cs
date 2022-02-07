using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YtStream.Models;

namespace YtStream
{
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


        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Locked = false;
            Configuration = configuration;
            BasePath = env.ContentRootPath;
            var C = ConfigModel.Load();
            if (C.UseCache)
            {
                Cache.SetBaseDirectory(C.CachePath);
            }
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}

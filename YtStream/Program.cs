using AyrA.AutoDI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Reflection;
using YtStream.Models;

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
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
            {
                Args = args,
                ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
            });

            //Adds file based logger
            builder.Services.AddLogging(loggingBuilder =>
            {
                var basePath = builder.Configuration.GetValue<string>("Config:BasePath");
                loggingBuilder.AddFile(@"{1}\Logs\app_{0:yyyy}-{0:MM}-{0:dd}.log", fileLoggerOpts =>
                {
                    fileLoggerOpts.FormatLogFileName = fName =>
                    {
                        return string.Format(fName, DateTime.UtcNow, basePath);
                    };
                });
            });

            //Register as a windows service
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "YtStream";
            });

            //Configure cookie based authentication
            builder.Services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
            });
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

            //Autoregister services
            AutoDIExtensions.DebugLogging = builder.Environment.IsDevelopment();
            builder.Services.AutoRegisterCurrentAssembly();

            //Add Swagger
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "YtStream Streaming API",
                    Description = "API endpoint for streaming files",
                });

                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });

            //Give us access to the IUrlHelper in services
            builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>()
                .AddScoped(x =>
                    x.GetRequiredService<IUrlHelperFactory>()
                        .GetUrlHelper(x.GetRequiredService<IActionContextAccessor>().ActionContext ?? throw null!));

            // Add services to the container.
            var mvc = builder.Services.AddControllersWithViews();

            if (builder.Environment.IsDevelopment())
            {
                mvc.AddRazorRuntimeCompilation();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                ErrorViewModel.DefaultDetailOption = true;
            }
            else
            {
                app.UseExceptionHandler("/Home/Exception");
                ErrorViewModel.DefaultDetailOption = false;
            }
            app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

            //Enable API browser
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

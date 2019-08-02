using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Enrichers.AspnetcoreHttpcontext;
using StartedSerilog.Core;

namespace StartedSerilog.WebUI
{
    public class Program
    {
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        public static int Main(string[] args)
        {
            //var name = Assembly.GetExecutingAssembly().GetName();
            //Log.Logger = new LoggerConfiguration()
            //    .MinimumLevel.Debug()
            //    //do not show microsoft information log when start it.
            //    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            //    .Enrich.FromLogContext()
            //    .Enrich.WithMachineName()
            //    .Enrich.WithProperty("Assembly",$"{name.Name}")
            //    .Enrich.WithProperty("Version",$"{name.Version}")
            //    .WriteTo.File(new RenderedCompactJsonFormatter(), @"C:\Users\Amir\source\Log\Serilog.json")
            //    .CreateLogger();
            try
            {
                CreateWebHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog((provider, context, loggerConfig) =>
                {
                    loggerConfig.WithSimpleConfiguration(provider, "SimpleUI", Configuration);
                });
        }
    }
}

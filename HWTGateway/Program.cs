using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.AspNetCore;
using NLog.Web;
using System;
using System.Collections.Generic;
using WalkingTec.Mvvm.Core;

namespace HWTGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //modified by qxg
            Console.WriteLine(@"!!!!!!!!!!!!!!!!!!!! HWTGateway starting !!!!!!!!!!!!!!!!!!!! ");

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return
                Host.CreateDefaultBuilder(args)
                  .ConfigureAppConfiguration((hostingContext, config) =>
                  {
                      config.AddInMemoryCollection(new Dictionary<string, string> { { "HostRoot", hostingContext.HostingEnvironment.ContentRootPath } });
                  })
                 .ConfigureLogging((hostingContext, logging) =>
                 {
                     logging.ClearProviders();
                     logging.AddConsole();
                     logging.AddWTMLogger();
                 })
                  .ConfigureWebHostDefaults(webBuilder =>
                 {
                     webBuilder.UseStartup<Startup>();
                     webBuilder.UseKestrel(option =>
                     {
                         //modified by qxg
                         //option.ListenAnyIP(1888, l => l.UseMqtt());
                         //option.ListenAnyIP(518);
                         option.ListenAnyIP(1886, l => l.UseMqtt());
                         option.ListenAnyIP(516);
                     });
                 })
                 .UseNLog();
        }
    }
}
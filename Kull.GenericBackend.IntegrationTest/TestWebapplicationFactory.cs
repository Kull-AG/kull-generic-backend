using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Kull.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend.IntegrationTest;

public class TestWebApplicationFactory<T>
    : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<T>
    where T : class
{
    protected override IWebHostBuilder CreateWebHostBuilder()
    {
        return Microsoft.AspNetCore.WebHost.CreateDefaultBuilder()
            .UseStartup<T>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {

        builder.ConfigureServices(services =>
        {
#if NETSTD2
                var hostEnv = (Microsoft.AspNetCore.Hosting.IHostingEnvironment)services.FirstOrDefault(f => f.ServiceType == typeof(Microsoft.AspNetCore.Hosting.IHostingEnvironment)).ImplementationInstance;
#else
                var hostEnv = (IWebHostEnvironment)services.FirstOrDefault(f => f.ServiceType == typeof(IWebHostEnvironment)).ImplementationInstance;
#endif
                var config = (IConfiguration)services.First(f => f.ServiceType == typeof(IConfiguration)).ImplementationInstance;
                // Not nice, but it seems as of .net core 3 this is required
                if (config == null)
            {
                config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", true, true)
                    .Build();
            }
            var constr = config["ConnectionStrings:DefaultConnection"];
            constr = constr.Replace("{{workdir}}", hostEnv.ContentRootPath);


            Utils.DatabaseUtils.SetupDb(hostEnv.ContentRootPath, constr);

                // Build the service provider.
                var sp = services.BuildServiceProvider();
        });
    }
}

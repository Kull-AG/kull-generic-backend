using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AspNetCore.Scalar;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.Standalone
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var hostEnv = (IWebHostEnvironment)services.FirstOrDefault(f => f.ServiceType == typeof(IWebHostEnvironment)).ImplementationInstance;
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

            services.AddRouting();
            services.AddMvc(config =>
            {
            });

            services.AddGenericBackend()
                .ConfigureMiddleware(m =>
                {
                    m.Prefix = "/rest";
                })
                .ConfigureOpenApiGeneration(o =>
                {
                    o.PersistResultSets = true;
                })
                .AddFileSupport()
                .AddSystemParameters();
            services.AddOpenApi(options =>
            {
                options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;

                options.AddGenericBackend();
            });
            if (!DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out var _))
                DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            services.AddScoped(typeof(DbConnection), (s) =>
            {
                var conf = s.GetRequiredService<IConfiguration>();
                var constr = conf["ConnectionStrings:DefaultConnection"];
                return Kull.Data.DatabaseUtils.GetConnectionFromEFString(constr, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            });

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseCors("AllowAll");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapOpenApi("/swagger/v1/swagger.json");


                app.UseGenericBackend(endpoints);
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });


            app.UseScalar(options =>
            {
                options.UseSpecUrl("/swagger/v1/swagger.json");
                options.UseTheme(Theme.Solarized);
            });

            app.UseStaticFiles();
            app.UseDefaultFiles();
        }
    }
}

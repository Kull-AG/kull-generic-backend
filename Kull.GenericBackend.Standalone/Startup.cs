using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
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
                .AddXmlSupport()
                .AddSystemParameters();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                c.AddGenericBackend();
            });
            if (!DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out var _))
                DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            services.AddScoped(typeof(DbConnection), (s) =>
            {
                var conf = s.GetRequiredService<IConfiguration>();
                var constr = conf["ConnectionStrings:DefaultConnection"];
                return Kull.Data.DatabaseUtils.GetConnectionFromEFString(constr, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger(o =>
            {
                // For compat with ng-swagger-gen on client. You can use ng-openapi-gen if set to false
                o.SerializeAsV2 = false;
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                app.UseGenericBackend(endpoints);
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseStaticFiles();
            app.UseDefaultFiles();
        }
    }
}

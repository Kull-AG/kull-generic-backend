using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend.IntegrationTest
{
    public class TestStartup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(config =>
            {
            });

            services.AddGenericBackend(null, new Kull.GenericBackend.SwaggerGeneration.SwaggerFromSPOptions()
            {

            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                c.AddGenericBackend();
            });
            services.AddTransient(typeof(DbConnection), (s) =>
            {
                var conf = s.GetRequiredService<IConfiguration>();
                var hostenv = s.GetRequiredService<IHostingEnvironment>();
                var constr = conf["ConnectionStrings:DefaultConnection"];
                constr = constr.Replace("{{workdir}}", hostenv.ContentRootPath);
                return Kull.Data.DatabaseUtils.GetConnectionFromEFString(constr, true);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSwagger(o =>
            {
                // For compat with ng-swagger-gen on client. You can use ng-openapi-gen if set to false
                o.SerializeAsV2 = true;
            });

            app.UseMvc(routeBuilder =>
            {
                app.UseGenericBackend(routeBuilder);
            });
            app.UseStaticFiles();
            app.UseDefaultFiles();

        }
    }
}

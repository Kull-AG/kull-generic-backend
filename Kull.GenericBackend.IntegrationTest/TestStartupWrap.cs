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
    public class TestStartupWrap
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
                    m.AlwaysWrapJson=true;
                    m.Prefix = "/rest";
                })
                .ConfigureOpenApiGeneration(o =>
                {
                    o.PersistResultSets = true;
                    o.ParameterFieldsAreRequired = true;
                    o.ResponseFieldsAreRequired = true;
                    o.UseSwagger2 = false;
                })
                .AddFileSupport()
                .AddXmlSupport()
                .AddSystemParameters(cf =>
                {
                    cf.AddSystemParameter("[Procedure with - strange name].ImASpecialParameter", (c) => true);
                });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                c.AddGenericBackend();
            });
            if (!DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out var _))
                DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            services.AddTransient<Filter.IRequestInterceptor, TestRequestInterceptor>();
            services.AddScoped(typeof(DbConnection), (s) =>
            {
                var conf = s.GetRequiredService<IConfiguration>();
                var hostenv = s.GetRequiredService<Microsoft.AspNetCore.Hosting.IHostingEnvironment>();
                var constr = conf["ConnectionStrings:DefaultConnection"];
                constr = constr.Replace("{{workdir}}", hostenv.ContentRootPath);

                return Kull.Data.DatabaseUtils.GetConnectionFromEFString(constr, true);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseSwagger(o =>
            {
                // For compat with ng-swagger-gen on client. You can use ng-openapi-gen if set to false
                o.SerializeAsV2 = false;
            });

#if NETSTD2
            app.UseMvc(routeBuilder =>
            {
                app.UseGenericBackend(routeBuilder);
            });
#else
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                app.UseGenericBackend(endpoints);
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

#endif
            app.UseStaticFiles();
            app.UseDefaultFiles();

        }
    }
}

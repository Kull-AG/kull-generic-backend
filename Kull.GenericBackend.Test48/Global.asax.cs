using Swashbuckle.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Kull.GenericBackend.Test48
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            UnityConfig.RegisterServices();
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            GlobalConfiguration.Configure(http =>
            {
                //Swagger is added for testing purposes 
                // http://localhost:50726/swagger/docs/v1
                // http://localhost:50726/swagger/ui/index
                http.EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "Api");
                    c.PrettyPrint();
                    c.AddGenericBackend();

                    //if (System.IO.File.Exists(Server.MapPath("~/bin/EmeraldVentures.XML")))
                    //    c.IncludeXmlComments(Server.MapPath("~/bin/EmeraldVentures.XML"));
                })
                .EnableSwaggerUi();

            });
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes); 
        }
    }
}

#if NETSTD2
using Microsoft.AspNetCore.Routing;
#else 
using Microsoft.AspNetCore.Builder;
using IRouteBuilder = Microsoft.AspNetCore.Routing.IEndpointRouteBuilder;
#endif
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.GenericSP
{
    class MiddlewareRegistration
    {
        private List<Entity> entities;
        private SPMiddlewareOptions options;

        public MiddlewareRegistration(IConfiguration conf)
        {

            var ent = conf.GetSection("Entities");
            entities = ent.GetChildren()
                   .Select(s => Entity.GetFromSection(s)).ToList();
        }

        /// <summary>
        /// Registers the actual middlware
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="routeBuilder">The routebuilder</param>
        protected internal void RegisterMiddleware(SPMiddlewareOptions options,
                IRouteBuilder routeBuilder)
        {
            this.options = options;
            foreach (var ent in entities)
            {
                Microsoft.AspNetCore.Http.RequestDelegate requestDelegate = context =>
                {
                    var srv = (IGenericSPMiddleware)context.RequestServices.GetService(typeof(IGenericSPMiddleware));
                    return srv.HandleRequest(context, ent);
                };
                if (ent.Methods.ContainsKey("GET"))
                {
                    routeBuilder.MapGet(GetUrlForMvcRouting(ent), requestDelegate);
                }
                if (ent.Methods.ContainsKey("PUT"))
                {
                    routeBuilder.MapPut(GetUrlForMvcRouting(ent), requestDelegate);
                }
                if (ent.Methods.ContainsKey("POST"))
                {
                    routeBuilder.MapPost(GetUrlForMvcRouting(ent), requestDelegate);
                }
                if (ent.Methods.ContainsKey("DELETE"))
                {
                    routeBuilder.MapDelete(GetUrlForMvcRouting(ent), requestDelegate);
                }
            }
        }


        private string GetUrlForMvcRouting(Entity ent)
        {
            var url = ent.GetUrl(options.Prefix, true);
            if (url.StartsWith("/"))
                return url.Substring(1);
            return url;
        }
    }
}

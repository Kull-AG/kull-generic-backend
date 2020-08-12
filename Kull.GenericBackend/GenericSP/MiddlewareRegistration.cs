#if NETFX
using System.Web.Routing;
using Unity;
#elif NETSTD2
using Microsoft.AspNetCore.Routing;
#else 
using Microsoft.AspNetCore.Builder;
using IRouteBuilder = Microsoft.AspNetCore.Routing.IEndpointRouteBuilder;
#endif
using System;
using System.Collections.Generic;
using Kull.GenericBackend.Common;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Config;
using System.Web;
using System.Threading.Tasks;

namespace Kull.GenericBackend.GenericSP
{
    class MiddlewareRegistration
    {
        private IReadOnlyList<Entity> entities;
        private SPMiddlewareOptions? options;

        public MiddlewareRegistration(ConfigProvider configProvider)
        {
            entities = configProvider.Entities;
        }

#if NET47
        class RouteHandlerWrap : HttpTaskAsyncHandler, IRouteHandler
        {
            Entity entity;
            public RouteHandlerWrap(Entity entity)
            {
                this.entity = entity;
            }
            public override bool IsReusable => false;

            public IHttpHandler GetHttpHandler(RequestContext requestContext)
            {
                return this;
            }

            public override async Task ProcessRequestAsync(HttpContext context)
            {
                var srv = (IGenericSPMiddleware)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(IGenericSPMiddleware));
                await srv.HandleRequest(new System.Web.HttpContextWrapper(context), this.entity);
            }
        }
        protected internal void RegisterMiddleware(SPMiddlewareOptions options,
                RouteCollection routeBuilder)
        {
            this.options = options;

            foreach (var ent in entities)
            {
                routeBuilder.Add(ent.ToString(),
                    new Route(GetUrlForMvcRouting(ent), new RouteHandlerWrap(ent)));

            }
        }
#else
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
                foreach (var method in ent.Methods)
                {
                    switch (method.Key)
                    {
                        case OperationType.Get:
                            routeBuilder.MapGet(GetUrlForMvcRouting(ent), requestDelegate);
                            break;
                        case OperationType.Put:
                            routeBuilder.MapPut(GetUrlForMvcRouting(ent), requestDelegate);
                            break;
                        case OperationType.Post:
                            routeBuilder.MapPost(GetUrlForMvcRouting(ent), requestDelegate);
                            break;
                        case OperationType.Delete:
                            routeBuilder.MapDelete(GetUrlForMvcRouting(ent), requestDelegate);
                            break;
                        case OperationType.Patch:
                            // TODO: Testing

#if NETSTD2
                            routeBuilder.MapVerb("PATCH", GetUrlForMvcRouting(ent), requestDelegate);
#else
                            routeBuilder.Map(GetUrlForMvcRouting(ent), context =>
                            {
                                if (context.Request.Method.ToUpper() == "PATCH")
                                {
                                    return requestDelegate(context);
                                }
                                return null;
                            });
#endif
                            break;
                        default:
                            throw new InvalidOperationException("Only Get, Pust, Post and Delete are allowed");
                    }
                }
            }
        }
#endif



        private string GetUrlForMvcRouting(Entity ent)
        {
            if (options == null) throw new InvalidOperationException("Must register first");
            var url = ent.GetUrl(options.Prefix, true);
            if (url.StartsWith("/"))
                return url.Substring(1);
            return url;
        }
    }
}

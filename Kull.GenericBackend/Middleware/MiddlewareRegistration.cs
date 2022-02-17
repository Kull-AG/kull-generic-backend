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
using System.Linq;
using System.Threading.Tasks;
#if NET48
using System.Web;
#endif

namespace Kull.GenericBackend.Middleware;

public class MiddlewareRegistration
{
    private IReadOnlyList<Entity> entities;
    private SPMiddlewareOptions? options;

    public MiddlewareRegistration(ConfigProvider configProvider)
    {
        entities = configProvider.Entities;
    }

#if NET48
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
            var container = (IUnityContainer)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(IUnityContainer));
            if (container != null)
            {
                using (var child = container.CreateChildContainer())
                {
                    var srv = child.Resolve<IGenericSPMiddleware>();
                    await srv.HandleRequest(new System.Web.HttpContextWrapper(context), this.entity);
                }
            }
            else
            {
                var srv = (IGenericSPMiddleware)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(IGenericSPMiddleware));
                await srv.HandleRequest(new System.Web.HttpContextWrapper(context), this.entity);
            }
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
                var srv = (IGenericSPMiddleware?)context.RequestServices.GetService(typeof(IGenericSPMiddleware));
                if (srv == null)
                {
                    throw new InvalidOperationException("IGenericSPMiddleware not given");
                }
                return srv.HandleRequest(context, ent);
            };
            foreach (var method in ent.Methods)
            {
                IEndpointConventionBuilder endpoint;
                switch (method.Key)
                {
                    case OperationType.Get:
                        endpoint = routeBuilder.MapGet(GetUrlForMvcRouting(ent), requestDelegate);
                        break;
                    case OperationType.Put:
                        endpoint = routeBuilder.MapPut(GetUrlForMvcRouting(ent), requestDelegate);
                        break;
                    case OperationType.Post:
                        endpoint = routeBuilder.MapPost(GetUrlForMvcRouting(ent), requestDelegate);
                        break;
                    case OperationType.Delete:
                        endpoint = routeBuilder.MapDelete(GetUrlForMvcRouting(ent), requestDelegate);
                        break;
                    case OperationType.Patch:
                        endpoint = routeBuilder.Map(GetUrlForMvcRouting(ent), context =>
                        {
                            if (context.Request.Method.ToUpper() == "PATCH")
                            {
                                return requestDelegate(context);
                            }
                            return null;
                        });
                        break;
                    default:
                        throw new InvalidOperationException("Only Get, Pust, Post and Delete are allowed");
                }
                if (method.Value.Policies != null || (options.Policies.Count>0))
                {
                    string[] policies = (method.Value.Policies ?? options.Policies).ToArray();
                    endpoint.RequireAuthorization(policies);
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

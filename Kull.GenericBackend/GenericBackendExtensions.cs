using Kull.DatabaseMetadata;
using Kull.GenericBackend.Parameters;
using Kull.GenericBackend.Serialization;
using Kull.GenericBackend.SwaggerGeneration;
#if NETFX 
using IServiceCollection = Unity.IUnityContainer;
using Kull.MvcCompat;
#else 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#endif
#if !NETSTD2 && !NETFX
using IRouteBuilder = Microsoft.AspNetCore.Routing.IEndpointRouteBuilder;
#endif

namespace Kull.GenericBackend
{
    /// <summary>
    /// Extension method for Swashbuckle
    /// </summary>
    public static class SwashbuckleExtensions
    {
#if NETFX
        public static void AddGenericBackend(this Swashbuckle.Application.SwaggerDocsConfig options)
        {
            options.DocumentFilter<DatabaseOperationWrap>();

        }
#else
        public static void AddGenericBackend(this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
        {
            options.DocumentFilter<DatabaseOperations>();
           
        }
#endif

    }

    /// <summary>
    /// Extension methods for MVC/Services
    /// </summary>
    public static class GenericBackendExtensions
    {
        public static GenericBackendBuilder AddGenericBackend(this IServiceCollection services)
        {
#if !NETFX
            services.AddRouting();
#endif
            services.AddKullDatabaseMetadata();
            services.TryAddSingleton<Common.NamingMappingHandler>();
            services.TryAddSingleton<SwaggerGeneration.CodeConvention>();
            services.TryAddSingleton<Config.ConfigProvider>();
            services.TryAddTransient<ParameterProvider>();
            services.TryAddSingleton<GenericSP.MiddlewareRegistration>();
            services.TryAddTransient<Execution.CommandPreparation>();
#if NETFX || NETSTD
            services.AddTransient<IGenericSPSerializer, GenericSPJsonSerializerJsonNet>();
#else
            services.AddTransient<IGenericSPSerializer, GenericSPJsonSerializerSTJ>();
#endif
            services.AddTransient<SerializerResolver, SerializerResolver>();
            services.AddTransient<GenericSP.IGenericSPMiddleware, GenericSP.GenericSPMiddleware>();
            services.AddTransient<Error.IResponseExceptionHandler, Error.SqlServerExceptionHandler>();
            GenericSP.SPMiddlewareOptions? options = null;
            SwaggerFromSPOptions? swaggerFromSPOptions = null;
            
            var opts = options ??
                    new GenericSP.SPMiddlewareOptions();
            services.TryAddSingleton(opts);
            services.TryAddSingleton(swaggerFromSPOptions ?? new SwaggerGeneration.SwaggerFromSPOptions());
#if NETFX
            services.AddTransient<Swashbuckle.Swagger.IDocumentFilter, DatabaseOperations>();
#endif
            return new GenericBackendBuilder(services);
        }

#if NETFX
        public static void UseGenericBackend(this System.Web.Routing.RouteCollection routeBuilder
           )
        {

            var service = (GenericSP.MiddlewareRegistration)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(GenericSP.MiddlewareRegistration));
            var opts = (GenericSP.SPMiddlewareOptions)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(GenericSP.SPMiddlewareOptions));
            service.RegisterMiddleware(opts, routeBuilder);
        }
#else

        public static void UseGenericBackend(
            this IApplicationBuilder applicationBuilder,
            IRouteBuilder routeBuilder
            )
        {
            var service = applicationBuilder.ApplicationServices.GetService<GenericSP.MiddlewareRegistration>();
            var opts = applicationBuilder.ApplicationServices.GetService<GenericSP.SPMiddlewareOptions>();
            service.RegisterMiddleware(opts, routeBuilder);
        }
#endif
    }
}

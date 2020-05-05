using Kull.DatabaseMetadata;
using Kull.GenericBackend.Parameters;
using Kull.GenericBackend.Serialization;
using Kull.GenericBackend.SwaggerGeneration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
#if !NETSTD2
using IRouteBuilder = Microsoft.AspNetCore.Routing.IEndpointRouteBuilder;
#endif
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend
{
    /// <summary>
    /// Extension method for Swashbuckle
    /// </summary>
    public static class SwashbuckleExtensions
    {
        public static void AddGenericBackend(this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
        {
            options.DocumentFilter<SwaggerGeneration.DatabaseOperations>();
        }
    }

    /// <summary>
    /// Extension methods for MVC/Services
    /// </summary>
    public static class GenericBackendExtensions
    {
        public static GenericBackendBuilder AddGenericBackend(this IServiceCollection services)
        {
            services.AddRouting();
            services.AddKullDatabaseMetadata();
            services.AddSingleton<Model.NamingMappingHandler>();
            services.AddSingleton<SwaggerGeneration.CodeConvention>();
            services.AddTransient<ParameterProvider>();
            services.AddSingleton<GenericSP.MiddlewareRegistration>();

            services.AddTransient<IGenericSPSerializer, GenericSPJsonSerializer>();
            services.AddTransient<SerializerResolver, SerializerResolver>();
            services.AddTransient<GenericSP.IGenericSPMiddleware, GenericSP.GenericSPMiddleware>();
            services.AddTransient<Error.IResponseExceptionHandler, Error.SqlServerExceptionHandler>();
            GenericSP.SPMiddlewareOptions? options = null;
            SwaggerFromSPOptions? swaggerFromSPOptions = null;
            var opts = options ??
                    new GenericSP.SPMiddlewareOptions();
            services.AddSingleton(opts);
            services.AddSingleton(swaggerFromSPOptions ?? new SwaggerGeneration.SwaggerFromSPOptions());
            return new GenericBackendBuilder(services);
        }


        public static void UseGenericBackend(
            this IApplicationBuilder applicationBuilder,
            IRouteBuilder routeBuilder
            )
        {
            var service = applicationBuilder.ApplicationServices.GetService<GenericSP.MiddlewareRegistration>();
            var opts = applicationBuilder.ApplicationServices.GetService<GenericSP.SPMiddlewareOptions>();

            service.RegisterMiddleware(opts, routeBuilder);

        }
    }
}

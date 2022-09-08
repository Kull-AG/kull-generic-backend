using System;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Parameters;
using Kull.GenericBackend.Serialization;
using Kull.GenericBackend.SwaggerGeneration;
#if NETFX 
using Unity;
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
namespace Kull.GenericBackend;

/// <summary>
/// Extension method for Swashbuckle
/// </summary>
public static class SwashbuckleExtensions
{
#if NETFX
    public static void AddGenericBackend(this Swashbuckle.Application.SwaggerDocsConfig options, IUnityContainer unityContainer)
    {
        options.DocumentFilter(() => new DatabaseOperationWrap(unityContainer));

    }

    [Obsolete("Use overload with container")]
    public static void AddGenericBackend(this Swashbuckle.Application.SwaggerDocsConfig options)
    {
        options.DocumentFilter(()=>new DatabaseOperationWrap());

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
        services.TryAddSingleton<Middleware.MiddlewareRegistration>();
        services.TryAddSingleton<Serialization.ResponseDescriptor>();
        services.TryAddTransient<Execution.CommandPreparation>();
#if NETFX || NETSTD
        services.AddTransient<IGenericSPSerializer, GenericSPJsonSerializerJsonNet>();
        services.TryAddSingleton<Middleware.IPolicyResolver>(new Middleware.ThrowPolicyResolver());
#else
        services.AddTransient<IGenericSPSerializer, GenericSPJsonSerializerSTJ>();
#endif
        services.AddTransient<IGenericSPSerializer, GenericSPNoneSerializer>();
        services.AddTransient<SerializerResolver, SerializerResolver>();
        services.AddTransient<Middleware.IGenericSPMiddleware, Middleware.GenericSPMiddleware>();
        services.AddTransient<Error.IResponseExceptionHandler, Error.SqlServerExceptionHandler>();
        services.AddTransient<Error.JsonErrorHandler, Error.JsonErrorHandler>();
        Middleware.SPMiddlewareOptions? options = null;
        SwaggerFromSPOptions? swaggerFromSPOptions = null;

        var opts = options ??
                new Middleware.SPMiddlewareOptions();
        services.TryAddSingleton(opts);
        services.TryAddSingleton(swaggerFromSPOptions ?? new SwaggerGeneration.SwaggerFromSPOptions());
#if NETFX
        services.AddTransient<Swashbuckle.Swagger.IDocumentFilter, DatabaseOperations>();
#endif
        return new GenericBackendBuilder(services);
    }

#if NETFX

    [Obsolete("Use overload with unity Container given. Has much better error handling")]
    public static void UseGenericBackend(this System.Web.Routing.RouteCollection routeBuilder)
    {
        var midm = System.Web.Mvc.DependencyResolver.Current.GetService(typeof(Middleware.MiddlewareRegistration));
        if (midm == null)
        {
            throw new System.InvalidOperationException("Must call AddGenericBackend on UnityContainer first");
        }

        var service = (Middleware.MiddlewareRegistration)midm;
        var opts = (Middleware.SPMiddlewareOptions)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(Middleware.SPMiddlewareOptions));
        service.RegisterMiddleware(opts, routeBuilder);
    }
    public static void UseGenericBackend(this System.Web.Routing.RouteCollection routeBuilder, Unity.IUnityContainer unityContainer)
    {
        var service = unityContainer.Resolve<Middleware.MiddlewareRegistration>();
        var opts = unityContainer.Resolve<Middleware.SPMiddlewareOptions>();
        service.RegisterMiddleware(opts, routeBuilder);
    }
#else

    public static void UseGenericBackend(
        this IApplicationBuilder applicationBuilder,
        IRouteBuilder routeBuilder
        )
    {
        var service = applicationBuilder.ApplicationServices.GetService<Middleware.MiddlewareRegistration>()!;
        var opts = applicationBuilder.ApplicationServices.GetService<Middleware.SPMiddlewareOptions>()!;
        service.RegisterMiddleware(opts, routeBuilder);
    }
#endif
#if NET6_0_OR_GREATER
    public static void UseGenericBackend(
        this Microsoft.AspNetCore.Builder.WebApplication applicationBuilder
        )
    {
        var service = applicationBuilder.Services.GetService<Middleware.MiddlewareRegistration>()!;
        var opts = applicationBuilder.Services.GetService<Middleware.SPMiddlewareOptions>()!;
        service.RegisterMiddleware(opts, applicationBuilder);
    }
#endif
}

using Kull.GenericBackend.Serialization;
using System;
using Kull.GenericBackend.SwaggerGeneration;
using System.Linq;
using Kull.GenericBackend.Middleware;
#if NETFX
using IServiceCollection = Unity.IUnityContainer;
using Unity;
using Kull.MvcCompat;
#else 
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

#endif

namespace Kull.GenericBackend;

public sealed class GenericBackendBuilder
{
    private IServiceCollection services;

    public GenericBackendBuilder(IServiceCollection services)
    {
        this.services = services;
    }
    public GenericBackendBuilder AddSerializer<T>() where T : class, IGenericSPSerializer
    {
        services.AddTransient<IGenericSPSerializer, T>();
        return this;
    }

    public GenericBackendBuilder AddSerializer<T>(Func<IServiceProvider, T> func) where T : class, IGenericSPSerializer
    {
        services.AddTransient<IGenericSPSerializer>(func);
        return this;
    }

    public GenericBackendBuilder AddParameterInterceptor<T>() where T : class, Filter.IParameterInterceptor
    {
        services.AddSingleton<Filter.IParameterInterceptor, T>();
        return this;
    }

    public GenericBackendBuilder AddParameterInterceptor<T>(Func<IServiceProvider, T> func) where T : class, Filter.IParameterInterceptor
    {
        services.AddSingleton<Filter.IParameterInterceptor>(func);
        return this;
    }


    public GenericBackendBuilder AddRequestLogger<T>() where T :  Filter.RequestLogger
    {
        services.AddTransient<Filter.RequestLogger, T>();
        return this;
    }

    public GenericBackendBuilder AddRequestLogger<T>(Func<IServiceProvider, T> func) where T : Kull.GenericBackend.Filter.RequestLogger
    {
        services.AddTransient<Filter.RequestLogger>(func);
        return this;
    }


    public GenericBackendBuilder AddRequestInterceptor<T>() where T : class, Filter.IRequestInterceptor
    {
        services.AddSingleton<Filter.IRequestInterceptor, T>();
        return this;
    }

    public GenericBackendBuilder AddRequestInterceptor<T>(Func<IServiceProvider, T> func) where T : class, Filter.IRequestInterceptor
    {
        services.AddSingleton<Filter.IRequestInterceptor>(func);
        return this;
    }


    public GenericBackendBuilder AddXmlSupport()
    {
        AddSerializer<GenericSPXmlSerializer>();
        return this;
    }

    public GenericBackendBuilder AddFileSupport()
    {
        AddSerializer<GenericSPFileSerializer>();
        AddParameterInterceptor<Filter.FileParameterInterceptor>();
        return this;
    }


    public GenericBackendBuilder AddSystemParameters(Action<Filter.SystemParameters>? configure = null)
    {
        AddParameterInterceptor<Filter.SystemParameters>((sp) =>
        {
            var sps = new Filter.SystemParameters();
            configure?.Invoke(sps);
            return sps;
        });
        return this;
    }

    /// <summary>
    /// Set options for the middleware
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public GenericBackendBuilder ConfigureMiddleware(Action<SPMiddlewareOptions> configure)
    {
#if NETFX
            SPMiddlewareOptions opts = (SPMiddlewareOptions)services.Resolve(typeof(SPMiddlewareOptions));
            configure(opts);
#else
        SPMiddlewareOptions opts = (SPMiddlewareOptions)services.First(s => s.ServiceType == typeof(SPMiddlewareOptions)).ImplementationInstance!;
        configure(opts);
#endif
        return this;
    }

    /// <summary>
    /// Set options for the generation of OpenApi Documents
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public GenericBackendBuilder ConfigureOpenApiGeneration(Action<SwaggerFromSPOptions> configure)
    {
#if NETFX
        SwaggerFromSPOptions opts = (SwaggerFromSPOptions)services.Resolve(typeof(SwaggerFromSPOptions));
        configure(opts);
#else
        SwaggerFromSPOptions opts = (SwaggerFromSPOptions)services.First(s => s.ServiceType == typeof(SwaggerFromSPOptions)).ImplementationInstance!;
        configure(opts);
#endif
        return this;
    }
}

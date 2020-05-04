using Kull.GenericBackend.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Kull.GenericBackend.SwaggerGeneration;
using System.Linq;
using Kull.GenericBackend.GenericSP;

namespace Kull.GenericBackend
{
    public sealed class GenericBackendBuilder
    {
        private IServiceCollection services;

        public GenericBackendBuilder(IServiceCollection services)
        {
            this.services = services;
        }
        public void AddSerializer<T>() where T : class, IGenericSPSerializer
        {
            services.AddTransient<IGenericSPSerializer, T>();
        }

        public void AddParameterInterceptor<T>() where T : class, Filter.IParameterInterceptor
        {
            services.AddSingleton<Filter.IParameterInterceptor, T>();
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

        public GenericBackendBuilder AddSystemParameters()
        {
            AddParameterInterceptor<Filter.SystemParameters>();
            return this;
        }


        public GenericBackendBuilder ConfigureMiddleware(Action<SPMiddlewareOptions> configure)
        {
            SPMiddlewareOptions opts = (SPMiddlewareOptions) services.First(s => s.ServiceType == typeof(SPMiddlewareOptions)).ImplementationInstance;
            configure(opts);
            return this;
        }

        public GenericBackendBuilder ConfigureOpenApiGeneration(Action<SwaggerFromSPOptions> configure)
        {
            SwaggerFromSPOptions opts = (SwaggerFromSPOptions)services.First(s => s.ServiceType == typeof(SwaggerFromSPOptions)).ImplementationInstance;
            configure(opts);
            return this;
        }
    }
}

using Kull.GenericBackend.Middleware;
using Kull.GenericBackend.SwaggerGeneration;
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
    public class TestStartupWrap : TestStartupBase
    {
        protected override bool UseSwaggerV2 => false;

        protected override void ConfigureMiddleware(SPMiddlewareOptions options)
        {
            base.ConfigureMiddleware(options);
            options.AlwaysWrapJson = true;
        }

        protected override void ConfigureOpenApi(SwaggerFromSPOptions options)
        {
            base.ConfigureOpenApi(options);
            options.ParameterFieldsAreRequired = true;
            options.ResponseFieldsAreRequired = true;
        }
    }

}

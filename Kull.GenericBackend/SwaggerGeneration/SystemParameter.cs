using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.SwaggerGeneration
{
    public class SystemParameter : WebApiParameter
    {
        private readonly Func<HttpContext, object?> getParameterValue;

        public SystemParameter(string sqlName,
              Func<HttpContext, object?> getParameterValue)
            : base(sqlName, null)
        {
            this.getParameterValue = getParameterValue ?? throw new ArgumentNullException(nameof(getParameterValue));
        }
        public override OpenApiSchema GetSchema()
        {
            return null!;
        }

        public override object? GetValue(HttpContext http, object? valueProvided)
        {
            var vl = getParameterValue(http);
            return vl;
        }
    }
}

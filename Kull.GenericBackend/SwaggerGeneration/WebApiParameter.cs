using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.SwaggerGeneration
{
    public abstract class WebApiParameter
    {
        public string SqlName { get; }

        public string WebApiName { get; }

        public abstract object GetValue(HttpContext http, object valueProvided);

        public WebApiParameter(string sqlName, string webApiName)
        {
            SqlName = sqlName;
            WebApiName = webApiName;
        }

        public virtual IEnumerable<WebApiParameter> GetRequiredTypes()
        {
            yield break;
        }

        public abstract OpenApiSchema GetSchema();
    }

}

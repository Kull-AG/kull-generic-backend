using Kull.GenericBackend.Common;
using Microsoft.AspNetCore.Http;

namespace Kull.GenericBackend.Filter
{
    /// <summary>
    /// Provides information for an Interceptor
    /// </summary>
    public class ParameterInterceptorContext
    {
        /// <summary>
        /// The entity, representing a url
        /// </summary>
        public Entity Entity { get; }
        public Method Method { get; }
        public HttpContext? HttpContext { get; }
        public bool IsFromOpenApiDefinition { get; }

        internal ParameterInterceptorContext(Entity ent, Method method, HttpContext? httpContext, bool isFromOpenApiDefinition)
        {
            this.Entity = ent;
            this.Method = method;
            HttpContext = httpContext;
            IsFromOpenApiDefinition = isFromOpenApiDefinition;
        }
    }
}

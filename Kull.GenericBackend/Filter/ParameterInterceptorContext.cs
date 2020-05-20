using Kull.GenericBackend.Common;

namespace Kull.GenericBackend.Filter
{
    /// <summary>
    /// Provides information for a Parameter Interceptor
    /// </summary>
    public class ParameterInterceptorContext
    {
        /// <summary>
        /// The entity, representing a url
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// The HTTP Method
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// True if from a request to the OpenApi Definition
        /// </summary>
        public bool IsFromOpenApiDefinition { get; }

        internal ParameterInterceptorContext(Entity ent, Method method, bool isFromOpenApiDefinition)
        {
            this.Entity = ent;
            this.Method = method;
            IsFromOpenApiDefinition = isFromOpenApiDefinition;
        }
    }
}

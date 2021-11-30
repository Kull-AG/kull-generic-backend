using Kull.GenericBackend.Common;
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
#if NET48
using System.Net.Http.Headers;
#else
using Microsoft.Net.Http.Headers;
#endif

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// An interface for writing the http response
    /// </summary>
    public interface IGenericSPSerializer
    {
        /// <summary>
        /// If there is an explicity set ResultType in appsettings, check for support
        /// </summary>
        /// <param name="resultType">The result type</param>
        /// <returns></returns>
        public bool SupportsResultType(string resultType);

        /// <summary>
        /// Indicates wheter the given content type is supported and the priority of this serializer
        /// </summary>
        /// <param name="contentTypes">The content type</param>
        /// <param name="entity">The entity of the request</param>
        /// <param name="method">The method of the request</param>
        /// <returns></returns>
        int? GetSerializerPriority(IEnumerable<MediaTypeHeaderValue> contentTypes,
            Entity entity,
            Method method);

        /// <summary>
        /// Writes the Result of the Command to the Body
        /// </summary>
        /// <param name="context">The http context</param>
        /// <param name="cmd">The command</param>
        /// <param name="method">The method</param>
        /// <param name="ent">The entity</param>
        /// <returns>The exception object if there was one that was handled. used for logging only</returns>
        Task<Exception?> ReadResultToBody(SerializationContext context);


        /// <summary>
        /// Hook to allow modifing the open api schema
        /// </summary>
        /// <param name="responses">The respones object</param>
        /// <param name="operationResponseContext">Some context</param>
        OpenApiResponses GetResponseType(SwaggerGeneration.OperationResponseContext operationResponseContext);
    }
}

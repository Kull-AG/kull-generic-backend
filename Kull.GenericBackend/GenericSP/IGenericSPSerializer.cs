using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kull.GenericBackend.GenericSP
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
        int? GetSerializerPriority(IList<Microsoft.Net.Http.Headers.MediaTypeHeaderValue> contentTypes, 
            Entity entity,
            Method method);

        /// <summary>
        /// Writes the Result of the Command to the Body
        /// </summary>
        /// <param name="context">The http context</param>
        /// <param name="cmd">The command</param>
        /// <param name="method">The method</param>
        /// <param name="ent">The entity</param>
        /// <returns></returns>
        Task ReadResultToBody(HttpContext context, System.Data.Common.DbCommand cmd, Method method, Entity ent);
    }
}

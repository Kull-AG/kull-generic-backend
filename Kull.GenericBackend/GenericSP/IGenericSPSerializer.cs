using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Kull.GenericBackend.GenericSP
{
    /// <summary>
    /// An interface for writing the http response
    /// </summary>
    public interface IGenericSPSerializer
    {
        /// <summary>
        /// Indicates wheter the given content type is supported
        /// </summary>
        /// <param name="contentType">The content type</param>
        /// <returns></returns>
        bool SupportContentType(Microsoft.Net.Http.Headers.MediaTypeHeaderValue contentType);

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
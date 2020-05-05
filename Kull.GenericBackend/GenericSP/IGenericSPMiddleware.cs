using Kull.GenericBackend.Common;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Kull.GenericBackend.GenericSP
{
    public interface IGenericSPMiddleware
    {
        /// <summary>
        /// Handles a given request
        /// </summary>
        /// <param name="context">The http Context</param>
        /// <param name="ent">The entity to process</param>
        /// <returns>A task</returns>
        Task HandleRequest(HttpContext context, Entity ent);
    }
}

using Kull.GenericBackend.Common;
#if NET47
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
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

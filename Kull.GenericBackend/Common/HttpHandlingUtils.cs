#if NET48
using HttpContext = System.Web.HttpContextBase;
using HttpResponse = System.Web.HttpResponseBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Common
{
    internal class HttpHandlingUtils
    {
        internal static async Task HttpContentToResponse(HttpContent content, HttpResponse response)
        {
#if NET48
            foreach (var h in content.Headers)
            {
                if (response.Headers.AllKeys.Contains(h.Key))
                {
                    response.Headers[h.Key] = h.Value.Single();
                }
                else
                {
                    response.Headers.Add(h.Key, h.Value.Single());
                }
            }
            await content.CopyToAsync(response.OutputStream).ConfigureAwait(false);
            await response.OutputStream.FlushAsync();
            await response.FlushAsync();
#else
            foreach (var h in content.Headers)
            {
                if (response.Headers.ContainsKey(h.Key))
                {
                    response.Headers[h.Key] = h.Value.ToArray();
                }
                else
                {
                    response.Headers.Add(h.Key, h.Value.ToArray());
                }
            }
            await content.CopyToAsync(response.Body).ConfigureAwait(false);
            await response.Body.FlushAsync();
#endif
        }
    }
}

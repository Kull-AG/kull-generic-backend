using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Common
{
    internal class HttpHandlingUtils
    {
        internal static async Task HttpContentToResponse(HttpContent content, HttpResponse response)
        {
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
        }
    }
}

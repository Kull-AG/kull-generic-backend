using Kull.GenericBackend.GenericSP;
using Kull.GenericBackend.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
#if NET48
using Kull.MvcCompat;
using HttpContext = System.Web.HttpContextBase;
using System.Net.Http.Headers;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Logging;
#endif

namespace Kull.GenericBackend.Error
{
    public class JsonErrorHandler
    {
        private readonly IEnumerable<IResponseExceptionHandler> errorHandlers;
        private readonly ILogger<JsonErrorHandler> logger;
        private readonly SPMiddlewareOptions options;

        public JsonErrorHandler(IEnumerable<Error.IResponseExceptionHandler> errorHandlers,
            ILogger<JsonErrorHandler> logger,
            SPMiddlewareOptions options)
        {
            this.errorHandlers = errorHandlers;
            this.logger = logger;
            this.options = options;
        }
        public async Task<bool> SerializeErrorAsJson(HttpContext context, Exception err,
            
            SerializationContext serializationContext            )
        {

#if NETFX
                logger.LogWarning($"Error executing {serializationContext} {err}");
#else
            logger.LogWarning(err, $"Error executing {serializationContext}");
#endif
            bool handled = false;
            foreach (var hand in errorHandlers)
            {
                var result = hand.GetContent(err, o =>
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(o);
                    var content = new System.Net.Http.StringContent(json, options.Encoding);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    return content;
                });
                if (result != null)
                {
                    (var status, var content) = result.Value;
#if NETFX
                        if (!context.Response.HeadersWritten)
#else
                    if (!context.Response.HasStarted)
#endif
                    {
                        context.Response.StatusCode = status;
                        context.Response.ContentType = "application/json; charset=" + options.Encoding.BodyName;
                        context.Response.Headers["Cache-Control"] = "no-store";
                        context.Response.Headers["Expires"] = "0";
                        await Common.HttpHandlingUtils.HttpContentToResponse(content, context.Response).ConfigureAwait(false);
                    }
                    else
                    {
                        logger.LogError(err, $"Could not execute {serializationContext} and could not handle error");
                    }
                    handled = true;
                    break;
                }

            }
            return handled;
        }
    }
}

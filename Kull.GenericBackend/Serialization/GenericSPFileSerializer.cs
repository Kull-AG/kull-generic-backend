using Kull.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
#if NETSTD2
using Newtonsoft.Json;
#else 
using System.Text.Json;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Common;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPFileSerializer : IGenericSPSerializer
    {
        public const string DefaultContentType = "application/octet-stream";

        protected string ContentColumn { get; } = "Content";
        protected string ContentTypeColumn { get; } = "ContentType";
        protected string FileNameColumn { get; } = "FileName";

        public bool SupportsResultType(string resultType) => resultType == "file";
        public int? GetSerializerPriority(IEnumerable<Microsoft.Net.Http.Headers.MediaTypeHeaderValue> contentTypes,
            Entity entity,
            Method method)
        {
            // Do not return null, Json is default/fallback
            return contentTypes.Any(contentType => contentType.MediaType == "application/octet-stream") ? 40 : 1001;
        }


        private readonly ILogger logger;
        private readonly IEnumerable<Error.IResponseExceptionHandler> errorHandlers;

        public GenericSPFileSerializer(
                ILogger<GenericSPJsonSerializer> logger,
                IEnumerable<Error.IResponseExceptionHandler> errorHandlers)
        {
            this.logger = logger;
            this.errorHandlers = errorHandlers;
        }

        /// <summary>
        /// Prepares the header
        /// </summary>
        /// <param name="context">The http context</param>
        /// <param name="method">The Http/SP mapping</param>
        /// <param name="ent">The Entity mapping</param>
        /// <returns></returns>
        protected Task PrepareHeader(HttpContext context, Method method, Entity ent, int statusCode, string contentType, string? fileName)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            if (statusCode == 200)
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    context.Response.Headers["Content-Disposition"] = "inline";
                }
                else
                {
                    SetContentAttachmentDisposition(context, fileName!);
                }
            }
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Expires"] = "0";

            return Task.CompletedTask;
        }

        protected void SetContentAttachmentDisposition(HttpContext context, string fileName)
        {
            // Thanks, https://stackoverflow.com/questions/93551/how-to-encode-the-filename-parameter-of-content-disposition-header-in-http
            string contentDisposition;
            string userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
            if (userAgent != null && userAgent.ToLowerInvariant().Contains("android")) // android built-in download manager (all browsers on android)
                contentDisposition = "attachment; filename=\"" + MakeAndroidSafeFileName(fileName) + "\"";
            else
                contentDisposition = "attachment; filename=\"" + fileName + "\"; filename*=UTF-8''" + Uri.EscapeDataString(fileName);
            context.Response.Headers["Content-Disposition"] = contentDisposition;
        }

        private static readonly Dictionary<char, char> AndroidAllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ._-+,@£$€!½§~'=()[]{}0123456789".ToDictionary(c => c);
        private string MakeAndroidSafeFileName(string fileName)
        {
            char[] newFileName = fileName.ToCharArray();
            for (int i = 0; i < newFileName.Length; i++)
            {
                if (!AndroidAllowedChars.ContainsKey(newFileName[i]))
                    newFileName[i] = '_';
            }
            return new string(newFileName);
        }

        /// <summary>
        /// Writes the result data to the body
        /// </summary>
        /// <param name="context">The HttpContext</param>
        /// <param name="cmd">The Db Command</param>
        /// <param name="method">The Http/SP mapping</param>
        /// <param name="ent">The Entity mapping</param>
        /// <returns>A Task</returns>
        public async Task ReadResultToBody(SerializationContext serializationContext)
        {
            var context = serializationContext.HttpContext;
            var method = serializationContext.Method;
            var ent = serializationContext.Entity;
            try
            {
                using (var rdr = await serializationContext.ExecuteReaderAsync())
                {
                    bool firstRead = rdr.Read();
                    if (!firstRead)
                    {
                        await PrepareHeader(context, method, ent, 404, "application/json", null);
                        return;
                    }
                    byte[] content = (byte[])rdr.GetValue(rdr.GetOrdinal(ContentColumn));
                    string? fileName = rdr.GetNString(FileNameColumn);
                    string contentType = rdr.GetNString(ContentTypeColumn) ?? DefaultContentType;

                    await PrepareHeader(context, method, ent, 200, contentType, fileName);
                    await context.Response.Body.WriteAsync(content, 0, content.Length);
                }

            }
            catch (Exception err)
            {
                bool handled = false;
                foreach (var hand in errorHandlers)
                {
                    var result = hand.GetContent(err);
                    if (result != null)
                    {
                        (var status, var content) = result.Value;
                        if (!context.Response.HasStarted)
                        {
                            await PrepareHeader(context, method, ent, status, "application/json", null);
                            foreach (var h in content.Headers)
                            {
                                context.Response.Headers.Add(h.Key, h.Value.ToArray());
                            }
                            await content.CopyToAsync(context.Response.Body).ConfigureAwait(false);
                        }
                        else
                        {
                            logger.LogError(err, $"Could not execute {serializationContext} and could not handle error");
                        }
                        handled = true;
                        break;
                    }

                }
                if (!handled)
                    throw;
            }
        }

        public void ModifyResponses(OpenApiResponses responses)
        {
            responses.Remove("200");
            responses.Add("200", new OpenApiResponse()
            {
                Description = "A binary file",
                Content = new Dictionary<string, OpenApiMediaType>(){
                        {
                            DefaultContentType,
                            new OpenApiMediaType()
                            {
                                Schema = new OpenApiSchema()
                                {
                                    // Actually in v3, type string would be correct, but I don't think this describes it correctly
                                    // https://swagger.io/docs/specification/describing-responses/
                                    Type = "file",
                                    Format = "binary"
                                }
                            }
                        }
                    }
            });
        }
    }
}

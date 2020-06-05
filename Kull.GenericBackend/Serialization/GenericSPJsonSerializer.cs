
#if NET47
using Kull.MvcCompat;
using HttpContext=System.Web.HttpContextBase;
using System.Net.Http.Headers;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
#endif
#if NETSTD2 || NETFX
using Newtonsoft.Json;
#else 
using System.Text.Json;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.GenericSP;
using Kull.GenericBackend.SwaggerGeneration;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializer : IGenericSPSerializer
    {
        public bool SupportsResultType(string resultType) => resultType == "json";
        public int? GetSerializerPriority(IEnumerable<MediaTypeHeaderValue> contentTypes,
            Entity entity,
            Method method)
        {
            // Do not return null, Json is default/fallback
            return contentTypes.Any(contentType => contentType.MediaType == "application/json" || contentType.MediaType == "*/*") ? 50 : 1000;
        }


        private readonly Common.NamingMappingHandler namingMappingHandler;
        private readonly SPMiddlewareOptions options;
        private readonly ILogger<GenericSPJsonSerializer> logger;
        private readonly IEnumerable<Error.IResponseExceptionHandler> errorHandlers;

        public GenericSPJsonSerializer(Common.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
                ILogger<GenericSPJsonSerializer> logger,
                IEnumerable<Error.IResponseExceptionHandler> errorHandlers)
        {
            this.namingMappingHandler = namingMappingHandler;
            this.options = options;
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
        protected Task PrepareHeader(HttpContext context, Method method, Entity ent, int statusCode)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=" + options.Encoding.BodyName;
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Expires"] = "0";
            return Task.CompletedTask;
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
#if !NETSTD2
            if (options.Encoding.BodyName != "utf-8")
            {
                throw new NotSupportedException("Only utf8 is supported");
            }
#endif
            try
            {
                using (var rdr = await serializationContext.ExecuteReaderAsync())
                {
                    bool firstRead = rdr.Read();
                    await PrepareHeader(context, method, ent, 200);
#if NETSTD2 
                    using (var jsonWriter = new JsonTextWriter(new System.IO.StreamWriter(context.Response.Body, options.Encoding)))
#elif NETFX 
                    using (var jsonWriter = new JsonTextWriter(new System.IO.StreamWriter(context.Response.OutputStream, options.Encoding)))
#else
                    await using (var jsonWriter = new Utf8JsonWriter(context.Response.Body))
#endif
                    {
                        string[] fieldNames = new string[rdr.FieldCount];

                        // Will store the types of the fields. Nullable datatypes will map to normal types
                        Type[] types = new Type[fieldNames.Length];
                        for (int i = 0; i < fieldNames.Length; i++)
                        {
                            fieldNames[i] = rdr.GetName(i);
                            types[i] = rdr.GetFieldType(i);
                            var nnType = Nullable.GetUnderlyingType(types[i]);
                            if (nnType != null)
                                types[i] = nnType;
                        }
                        fieldNames = namingMappingHandler.GetNames(fieldNames).ToArray();
#if !NETSTD2 && !NETFX
                        var fieldNamesToUse = fieldNames.Select(f => JsonEncodedText.Encode(f)).ToArray();
#else
                        var fieldNamesToUse = fieldNames;
#endif
                        jsonWriter.WriteStartArray();
                        while (firstRead || rdr.Read())
                        {
                            firstRead = false;
                            jsonWriter.WriteStartObject();
                            for (int p = 0; p < fieldNamesToUse.Length; p++)
                            {
#if NETSTD2 || NETFX
                                jsonWriter.WritePropertyName(fieldNamesToUse[p]);
                                var vl = rdr.GetValue(p);
                                jsonWriter.WriteValue(vl == DBNull.Value ? null : vl);
#else
                                if (rdr.IsDBNull(p))
                                {
                                    jsonWriter.WriteNull(fieldNamesToUse[p]);
                                }
                                else if (types[p] == typeof(string))
                                {
                                    jsonWriter.WriteString(fieldNamesToUse[p], rdr.GetString(p));
                                }
                                else if (types[p] == typeof(DateTime))
                                {
                                    jsonWriter.WriteString(fieldNamesToUse[p], rdr.GetDateTime(p));
                                }
                                else if (types[p] == typeof(DateTimeOffset))
                                {
                                    jsonWriter.WriteString(fieldNamesToUse[p], (DateTimeOffset)rdr.GetValue(p));
                                }
                                else if (types[p] == typeof(bool))
                                {
                                    jsonWriter.WriteBoolean(fieldNamesToUse[p], rdr.GetBoolean(p));
                                }
                                else if (types[p] == typeof(Guid))
                                {
                                    jsonWriter.WriteString(fieldNamesToUse[p], rdr.GetGuid(p));
                                }
                                else if (types[p] == typeof(short))
                                {
                                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetInt16(p));
                                }
                                else if (types[p] == typeof(int))
                                {
                                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetInt32(p));
                                }
                                else if (types[p] == typeof(long))
                                {
                                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetInt64(p));
                                }
                                else if (types[p] == typeof(float))
                                {
                                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetFloat(p));
                                }
                                else if (types[p] == typeof(double))
                                {
                                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetDouble(p));
                                }
                                else if (types[p] == typeof(decimal))
                                {
                                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetDecimal(p));
                                }
                                else if (types[p] == typeof(byte[]))
                                {
                                    jsonWriter.WriteBase64String(fieldNamesToUse[p], (byte[])rdr.GetValue(p));
                                }
                                else
                                {
                                    string? vl = rdr.GetValue(p)?.ToString();
                                    jsonWriter.WriteString(fieldNamesToUse[p], vl);
                                }

#endif



                            }
                            jsonWriter.WriteEndObject();
                        }
                        jsonWriter.WriteEndArray();
                        await jsonWriter.FlushAsync();
                    }
                }

            }
            catch (Exception err)
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
                    {                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(o);
                        var content = new System.Net.Http.StringContent(json);
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
                            await PrepareHeader(context, method, ent, status);
                            await HttpHandlingUtils.HttpContentToResponse(content, context.Response).ConfigureAwait(false);
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


        public OpenApiResponses ModifyResponses(OpenApiResponses responses, OperationResponseContext operationResponseContext)
        {
            return responses;
        }
    }
}

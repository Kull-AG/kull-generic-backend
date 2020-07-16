
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
using System.Data.Common;
using System.IO;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public abstract class GenericSPJsonSerializerBase : IGenericSPSerializer
    {
        public bool SupportsResultType(string resultType) => resultType == "json";
        public int? GetSerializerPriority(IEnumerable<MediaTypeHeaderValue> contentTypes,
            Entity entity,
            Method method)
        {
            // Do not return null, Json is default/fallback
            return contentTypes.Any(contentType => contentType.MediaType == "application/json" || contentType.MediaType == "*/*") ? 50 : 1000;
        }


        protected readonly Common.NamingMappingHandler namingMappingHandler;
        protected readonly SPMiddlewareOptions options;
        protected readonly ILogger logger;
        protected readonly IEnumerable<Error.IResponseExceptionHandler> errorHandlers;
        private readonly CodeConvention codeConvention;

        public GenericSPJsonSerializerBase(IServiceProvider serviceProvider)
        {
            this.namingMappingHandler = (Common.NamingMappingHandler)serviceProvider.GetService(typeof(Common.NamingMappingHandler));
            this.options = (SPMiddlewareOptions)serviceProvider.GetService(typeof(SPMiddlewareOptions));
            this.logger = (ILogger<GenericSPJsonSerializerBase>)serviceProvider.GetService(typeof(ILogger<GenericSPJsonSerializerBase>));
            this.errorHandlers = (IEnumerable<Error.IResponseExceptionHandler>)serviceProvider.GetService(typeof(IEnumerable<Error.IResponseExceptionHandler>)); 
            this.codeConvention = (CodeConvention)serviceProvider.GetService(typeof(CodeConvention));
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

        private void WriteOutputParameters(Stream outputStream, IReadOnlyCollection<DbParameter> outParameters)
        {
            Dictionary<string, object> outParameterValues
                = new Dictionary<string, object>(outParameters.Count);
            foreach (var p in outParameters.Cast<DbParameter>())
            {
                outParameterValues.Add(p.ParameterName.StartsWith("@") ? p.ParameterName.Substring(1) : p.ParameterName,
                    p.Value);

            }
            Kull.Data.DataReader.ObjectDataReader objectData = new Data.DataReader.ObjectDataReader(
                new Dictionary<string, object>[]
                {
                    outParameterValues
                }
                );
            objectData.Read();
            string[] fieldNames = new string[objectData.FieldCount];

            // Will store the types of the fields. Nullable datatypes will map to normal types
            for (int i = 0; i < fieldNames.Length; i++)
            {
                fieldNames[i] = objectData.GetName(i);
            }
            fieldNames = namingMappingHandler.GetNames(fieldNames).ToArray();
            WriteObject(outputStream, objectData, fieldNames);

        }

        private void WriteRaw(Stream stream, string value)
        {
            byte[] data = options.Encoding.GetBytes(value);
            stream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Writes an object to the given outputStream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="objectData"></param>
        /// <param name="fieldNames"></param>
        /// <returns></returns>
        protected abstract Task WriteObject(Stream outputStream, System.Data.IDataRecord objectData, string[] fieldNames);

        /// <summary>
        /// Writes a Json Array of the given Data to the underlying stream.
        /// The method must NOT dispose the stream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="reader"></param>
        /// <param name="fieldNames"></param>
        /// <param name="firstReadDone"></param>
        /// <returns></returns>
        protected abstract Task WriteCurrentResultSet(Stream outputStream, DbDataReader reader, string[] fieldNames, bool firstReadDone);

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
            var outParameters = serializationContext.GetParameters()
                .Where(p => p.Direction == System.Data.ParameterDirection.Output || p.Direction == System.Data.ParameterDirection.InputOutput)
                .ToArray();
            bool wrap = options.AlwaysWrapJson || outParameters.Length > 0;

            try
            {
                using (var rdr = await serializationContext.ExecuteReaderAsync())
                {
                    bool firstRead = rdr.Read();
                    await PrepareHeader(context, method, ent, 200);


                    string[] fieldNames = new string[rdr.FieldCount];

                    // Will store the types of the fields. Nullable datatypes will map to normal types
                    for (int i = 0; i < fieldNames.Length; i++)
                    {
                        fieldNames[i] = rdr.GetName(i);
                    }
                    fieldNames = namingMappingHandler.GetNames(fieldNames).ToArray();
#if NETFX
                    var stream = context.Response.OutputStream; ;
#else
                    var stream = context.Response.Body;
#endif
                    if (wrap)
                    {
                        WriteRaw(stream, $"{{ \"{codeConvention.FirstResultKey}\": \r\n");

                        await WriteCurrentResultSet(stream, rdr, fieldNames, true);

                        bool first = true;
                        bool hasAnyResults = false;
                        while (await rdr.NextResultAsync())
                        {
                            if (first)
                            {
                                WriteRaw(stream, $", \"{codeConvention.OtherResultsKey}\": [");
                                first = false;
                                hasAnyResults = true;
                            }
                            else
                            {
                                WriteRaw(stream, ",");
                            }
                            await WriteCurrentResultSet(stream, rdr, fieldNames, true);
                        }
                        if (hasAnyResults)
                        {
                            WriteRaw(stream, "]");
                        }
                        if (outParameters.Length > 0)
                        {
                            WriteRaw(stream, $", \"{codeConvention.OutputParametersKey}\": ");
                            WriteOutputParameters(stream, outParameters);
                        }
                        WriteRaw(stream, "\r\n}");
                    }
                    else
                    {
                        await WriteCurrentResultSet(stream, rdr, fieldNames, true);
                    }
                    await stream.FlushAsync();
#if NET47
                    await context.Response.FlushAsync();
#endif
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
                    {
                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(o);
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


        public virtual OpenApiResponses ModifyResponses(OpenApiResponses responses, OperationResponseContext operationResponseContext)
        {
            return responses;
        }
    }
}

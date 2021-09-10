
#if NET48
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
using Kull.GenericBackend.Error;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public abstract class GenericSPJsonSerializerBase : IGenericSPSerializer
    {
        public const string FirstResultSetType = "first";

        public bool SupportsResultType(string resultType) => resultType == "json" || FirstResultSetType == resultType;
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
        private readonly CodeConvention codeConvention;
        private readonly ResponseDescriptor responseDescriptor;
        private readonly JsonErrorHandler jsonErrorHandler;

        public GenericSPJsonSerializerBase(Common.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
                ILogger<GenericSPJsonSerializerBase> logger,
                CodeConvention convention,
                ResponseDescriptor responseDescriptor,
                Error.JsonErrorHandler jsonErrorHandler)
        {
            this.namingMappingHandler = namingMappingHandler;
            this.options = options;
            this.logger = logger;
            this.codeConvention = convention;
            this.responseDescriptor = responseDescriptor;
            this.jsonErrorHandler = jsonErrorHandler;
        }

        /// <summary>
        /// Prepares the header
        /// </summary>
        /// <param name="context">The http context</param>
        /// <param name="method">The Http/SP mapping</param>
        /// <param name="ent">The Entity mapping</param>
        /// <returns></returns>
        protected Task PrepareHeader(SerializationContext context, Method method, Entity ent, int statusCode)
        {
            context.SetHeaders("application/json; charset=" + options.Encoding.BodyName, statusCode, true);
            return Task.CompletedTask;
        }

        private async Task WriteOutputParameters(Stream outputStream, IReadOnlyCollection<DbParameter> outParameters)
        {
            Dictionary<string, object?> outParameterValues = outParameters.Cast<DbParameter>().ToDictionary(
                p => p.ParameterName.StartsWith("@") ? p.ParameterName.Substring(1) : p.ParameterName,
                  p => (object?)p.Value);

            Kull.Data.DataReader.ObjectDataReader objectData = new Data.DataReader.ObjectDataReader(
                new IReadOnlyDictionary<string, object?>[]
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
            await WriteObject(outputStream, objectData, fieldNames);

        }

        private async Task WriteRaw(Stream stream, string value)
        {
            byte[] data = options.Encoding.GetBytes(value);
            await stream.WriteAsync(data, 0, data.Length);
        }

        /// <summary>
        /// Writes an object to the given outputStream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="objectData"></param>
        /// <param name="fieldNames"></param>
        /// <returns></returns>
        protected abstract Task WriteObject(Stream outputStream, System.Data.IDataRecord objectData, string?[] fieldNames);

        /// <summary>
        /// Writes a Json Array of the given Data to the underlying stream.
        /// The method must NOT dispose the stream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="reader"></param>
        /// <param name="fieldNames"></param>
        /// <param name="firstReadResult"></param>
        /// <returns></returns>
        protected abstract Task WriteCurrentResultSet(Stream outputStream, DbDataReader reader, string?[] fieldNames, bool? firstReadResult, bool objectOfFirstOnly);

        /// <summary>
        /// Writes the result data to the body
        /// </summary>
        /// <param name="context">The HttpContext</param>
        /// <param name="cmd">The Db Command</param>
        /// <param name="method">The Http/SP mapping</param>
        /// <param name="ent">The Entity mapping</param>
        /// <returns>A Task</returns>
        public async Task<Exception?> ReadResultToBody(SerializationContext serializationContext)
        {
            var method = serializationContext.Method;
            var ent = serializationContext.Entity;
            var resultType = serializationContext.Method.ResultType;
            var outParameters = serializationContext.GetParameters()
                .Where(p => p.Direction == System.Data.ParameterDirection.Output || p.Direction == System.Data.ParameterDirection.InputOutput)
                .ToArray();
            bool wrap = options.AlwaysWrapJson || outParameters.Length > 0;

            try
            {
                using (var rdr = await serializationContext.ExecuteReaderAsync(resultType == FirstResultSetType ? System.Data.CommandBehavior.SingleRow :
                    System.Data.CommandBehavior.SequentialAccess))
                {
                    bool firstReadResult = rdr.Read();
                    await PrepareHeader(serializationContext, method, ent, 200);


                    string?[] fieldNames = new string[rdr.FieldCount];

                    // Will store the types of the fields. Nullable datatypes will map to normal types
                    for (int i = 0; i < fieldNames.Length; i++)
                    {
                        fieldNames[i] = rdr.GetName(i);
                    }
                    if (method.IgnoreFields.Count > 0)
                    {
                        fieldNames = fieldNames.Select(f => !method.IgnoreFields.Contains(f, StringComparer.OrdinalIgnoreCase) ?
                             f : NamingMappingHandler.IgnoreFieldPlaceHolder).ToArray();
                        fieldNames = namingMappingHandler.GetNames(fieldNames)
                            .Select(s => s == NamingMappingHandler.IgnoreFieldPlaceHolder ? null : s)
                            .ToArray();
                    }
                    else
                    {
                        fieldNames = namingMappingHandler.GetNames(fieldNames).ToArray();
                    }
                    var stream = serializationContext.OutputStream;
                    if (wrap)
                    {
                        await WriteRaw(stream, $"{{ \"{codeConvention.FirstResultKey}\": \r\n");

                        await WriteCurrentResultSet(stream, rdr, fieldNames, firstReadResult, resultType == FirstResultSetType);

                        bool first = true;
                        bool hasAnyResults = false;
                        while (await rdr.NextResultAsync())
                        {
                            if (first)
                            {
                                await WriteRaw(stream, $", \"{codeConvention.OtherResultsKey}\": [");
                                first = false;
                                hasAnyResults = true;
                            }
                            else
                            {
                                await WriteRaw(stream, ",");
                            }
                            await WriteCurrentResultSet(stream, rdr, fieldNames, false, resultType == FirstResultSetType);
                        }
                        if (hasAnyResults)
                        {
                            await WriteRaw(stream, "]");
                        }
                        if (outParameters.Length > 0)
                        {
                            await WriteRaw(stream, $", \"{codeConvention.OutputParametersKey}\": ");
                            await WriteOutputParameters(stream, outParameters);
                        }
                        await WriteRaw(stream, "\r\n}");
                    }
                    else
                    {
                        await WriteCurrentResultSet(stream, rdr, fieldNames, firstReadResult, resultType == FirstResultSetType);
                    }
                    await serializationContext.FlushResponseAsync();
                }
                return null;
            }
            catch (Exception err)
            {
                var handled = await jsonErrorHandler.SerializeErrorAsJson(err, serializationContext);

                if (!handled)
                    throw;
                else
                    return err;
            }
        }


        public virtual OpenApiResponses GetResponseType(OperationResponseContext operationResponseContext)
        {
            return responseDescriptor.GetDefaultResponse(operationResponseContext,
                        operationResponseContext.Method.ResultType == FirstResultSetType);
        }
    }
}

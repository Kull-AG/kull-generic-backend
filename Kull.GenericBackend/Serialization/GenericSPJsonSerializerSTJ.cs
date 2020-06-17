
#if !NETSTD && !NETFX
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.GenericSP;
using Kull.GenericBackend.SwaggerGeneration;
using System.IO;
using System.Data.Common;
using System.Text.Json;
using Kull.GenericBackend.Error;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializerSTJ : GenericSPJsonSerializerBase, IGenericSPSerializer
    {
        public GenericSPJsonSerializerSTJ(NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options, ILogger<GenericSPJsonSerializerBase> logger, IEnumerable<IResponseExceptionHandler> errorHandlers) : base(namingMappingHandler, options, logger, errorHandlers)
        {
        }

        protected override  async Task WriteCurrentResultSet(Stream outputStream, DbDataReader rdr, string[] fieldNamesToUse, bool firstReadDone)
        {

            if (options.Encoding.BodyName != "utf-8")
            {
                throw new NotSupportedException("Only utf8 is supported");
            }
            Type[] types = new Type[fieldNamesToUse.Length];
            for (int i = 0; i < fieldNamesToUse.Length; i++)
            {
                types[i] = rdr.GetFieldType(i);
                var nnType = Nullable.GetUnderlyingType(types[i]);
                if (nnType != null)
                    types[i] = nnType;
            }
            var jsonWriter = new Utf8JsonWriter(outputStream);
            jsonWriter.WriteStartArray();
            if (!firstReadDone)
                rdr.Read();


            do
            {
                jsonWriter.WriteStartObject();
                for (int p = 0; p < fieldNamesToUse.Length; p++)
                {
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
                }
                jsonWriter.WriteEndObject();
            }
            while (rdr.Read());

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync();
        }
    }
}
#endif

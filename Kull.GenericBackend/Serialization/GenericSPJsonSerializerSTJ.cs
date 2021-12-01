
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
using Kull.GenericBackend.Middleware;
using Kull.GenericBackend.SwaggerGeneration;
using System.IO;
using System.Data.Common;
using System.Text.Json;
using Kull.GenericBackend.Error;
using System.Data;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializerSTJ : GenericSPJsonSerializerBase, IGenericSPSerializer
    {
        public GenericSPJsonSerializerSTJ(Common.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
                ILogger<GenericSPJsonSerializerBase> logger,
                CodeConvention convention,
                ResponseDescriptor responseDescriptor,
                 Error.JsonErrorHandler jsonErrorHandler) : base(namingMappingHandler, options, logger,
                    convention, responseDescriptor, jsonErrorHandler)
        { }

        protected override async Task WriteCurrentResultSet(Stream outputStream, DbDataReader rdr,
            string?[] fieldNamesToUse, bool? firstReadResult, bool objectOfFirstOnly)
        {

            if (options.Encoding.BodyName != "utf-8")
            {
                throw new NotSupportedException("Only utf8 is supported");
            }
            Type[] types = GetTypesFromReader(rdr);
            var jsonWriter = new Utf8JsonWriter(outputStream);

            if (firstReadResult == null)
                firstReadResult = rdr.Read();
            var jsFields = fieldNamesToUse.Select(s => s == null ? null: (JsonEncodedText?) JsonEncodedText.Encode(s)).ToArray();
            if (objectOfFirstOnly)
            {
                if (firstReadResult.Value)
                {
                    await WriteSingleRow(rdr, jsFields, types, jsonWriter, outputStream);
                }
                else
                {
                    jsonWriter.WriteNullValue();
                }
                await jsonWriter.FlushAsync();
                return;
            }

            jsonWriter.WriteStartArray();


            if (firstReadResult == true)
            {
                do
                {
                    await WriteSingleRow(rdr, jsFields, types, jsonWriter, outputStream);
                }
                while (rdr.Read());
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync();
        }

        private static Type[] GetTypesFromReader(System.Data.IDataRecord rdr)
        {
            Type[] types = new Type[rdr.FieldCount];
            for (int i = 0; i < rdr.FieldCount; i++)
            {
                types[i] = rdr.GetFieldType(i);
                var nnType = Nullable.GetUnderlyingType(types[i]);
                if (nnType != null)
                    types[i] = nnType;
            }

            return types;
        }
#if NET6_0_OR_GREATER
        static JsonEncodedText charStart = JsonEncodedText.Encode("\"");

        char[] charBuffer;

        private byte[] GetJsonEncodedText(char[] input, int from, int maxChars)  {
            ReadOnlySpan<char> rsp = new ReadOnlySpan<char>(input, from, maxChars);
            return JsonEncodedText.Encode(rsp).EncodedUtf8Bytes.ToArray();
            }
#endif

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously. Suppress because of targeting
        protected async Task WriteSingleRow(System.Data.IDataRecord rdr, JsonEncodedText?[] fieldNamesToUse, Type[] types, Utf8JsonWriter jsonWriter, Stream baseStream)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            jsonWriter.WriteStartObject();
            for (int p = 0; p < fieldNamesToUse.Length; p++)
            {
                var fieldName = fieldNamesToUse[p];
                if (fieldName == null) continue;
                var realFieldName = fieldName.Value;
                if (rdr.IsDBNull(p))
                {
                    jsonWriter.WriteNull(realFieldName);
                }
                else if (types[p] == typeof(string))
                {
#if !NET6_0_OR_GREATER
                    jsonWriter.WriteString(realFieldName, rdr.GetString(p));
#else
                    jsonWriter.WritePropertyName(realFieldName);
                    byte[] charStart = new byte[] { ((byte)'"') };
                    jsonWriter.WriteRawValue(charStart, true);// We need this one call to WriteRawValue to have correcct internal state
                    await jsonWriter.FlushAsync();

                    
                    
                    charBuffer ??= new char[100];
                    long offset = 0;
                    int bytesRead = 0;
                    do
                    {
                        bytesRead = (int)rdr.GetChars(p, offset, charBuffer, 0, charBuffer.Length);
                        offset += bytesRead;
                        if (bytesRead > 0)
                        {
                            var j = GetJsonEncodedText(charBuffer, 0, bytesRead);                            
                            await baseStream.WriteAsync(j);
                        }
                    }
                    while (bytesRead > 0);
                    await baseStream.WriteAsync(charStart);
#endif
                    //rdr.GetChars()
                }
                else if (types[p] == typeof(DateTime))
                {
                    jsonWriter.WriteString(realFieldName, rdr.GetDateTime(p));
                }
                else if (types[p] == typeof(DateTimeOffset))
                {
                    jsonWriter.WriteString(realFieldName, (DateTimeOffset)rdr.GetValue(p));
                }
                else if (types[p] == typeof(bool))
                {
                    jsonWriter.WriteBoolean(realFieldName, rdr.GetBoolean(p));
                }
                else if (types[p] == typeof(Guid))
                {
                    jsonWriter.WriteString(realFieldName, rdr.GetGuid(p));
                }
                else if (types[p] == typeof(short))
                {
                    jsonWriter.WriteNumber(realFieldName, rdr.GetInt16(p));
                }
                else if (types[p] == typeof(int))
                {
                    jsonWriter.WriteNumber(realFieldName, rdr.GetInt32(p));
                }
                else if (types[p] == typeof(long))
                {
                    jsonWriter.WriteNumber(realFieldName, rdr.GetInt64(p));
                }
                else if (types[p] == typeof(float))
                {
                    jsonWriter.WriteNumber(realFieldName, rdr.GetFloat(p));
                }
                else if (types[p] == typeof(double))
                {
                    jsonWriter.WriteNumber(realFieldName, rdr.GetDouble(p));
                }
                else if (types[p] == typeof(decimal))
                {
                    jsonWriter.WriteNumber(realFieldName, rdr.GetDecimal(p));
                }
                else if (types[p] == typeof(byte[]))
                {
                    jsonWriter.WriteBase64String(realFieldName, (byte[])rdr.GetValue(p));
                }
                else
                {
                    string? vl = rdr.GetValue(p)?.ToString();
                    jsonWriter.WriteString(realFieldName, vl);
                }
            }
            jsonWriter.WriteEndObject();
        }

        protected override async Task WriteObject(Stream outputStream, IDataRecord objectData, string?[] fieldNames)
        {

            var jsonWriter = new Utf8JsonWriter(outputStream);
            var types = GetTypesFromReader(objectData);
            var jsFields = fieldNames.Select(s => s == null?null :(JsonEncodedText?) JsonEncodedText.Encode(s)).ToArray();
            await WriteSingleRow(objectData, jsFields, types, jsonWriter, outputStream);
            await jsonWriter.FlushAsync();
        }
    }
}
#endif


#if NET47
using Kull.MvcCompat;
using HttpContext=System.Web.HttpContextBase;
using System.Net.Http.Headers;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
#endif
using Newtonsoft.Json;
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
using Kull.GenericBackend.Error;
using Newtonsoft.Json.Serialization;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializerJsonNet : GenericSPJsonSerializerBase, IGenericSPSerializer
    {
        public GenericSPJsonSerializerJsonNet(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async Task WriteCurrentResultSet(Stream outputStream, DbDataReader rdr, string[] fieldNames, bool firstReadDone)
        {
            var streamWriter = new StreamWriter(outputStream, options.Encoding, 1024 * 8, leaveOpen: true);
            var jsonWriter = new JsonTextWriter(streamWriter);

            jsonWriter.WriteStartArray();
            if (!firstReadDone)
                rdr.Read();


            do
            {
                WriteSingleRow(rdr, fieldNames, jsonWriter);
            }
            while (rdr.Read());

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync();
            await streamWriter.FlushAsync();
        }

        private static void WriteSingleRow(System.Data.IDataRecord rdr, string[] fieldNames, JsonTextWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();
            for (int p = 0; p < fieldNames.Length; p++)
            {
                jsonWriter.WritePropertyName(fieldNames[p]);
                var vl = rdr.GetValue(p);
                jsonWriter.WriteValue(vl == DBNull.Value ? null : vl);
            }
            jsonWriter.WriteEndObject();
        }

        protected override async Task WriteObject(Stream outputStream, System.Data.IDataRecord objectData, string[] fieldNames)
        {
            var streamWriter = new StreamWriter(outputStream, options.Encoding, 1024 * 8, leaveOpen: true);
            var jsonWriter = new JsonTextWriter(streamWriter);
            WriteSingleRow(objectData, fieldNames, jsonWriter);
            await jsonWriter.FlushAsync();
            await streamWriter.FlushAsync();
        }
    }



}

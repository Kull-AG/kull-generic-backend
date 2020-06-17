
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

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializerJsonNet : GenericSPJsonSerializerBase, IGenericSPSerializer
    {
        public GenericSPJsonSerializerJsonNet(NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options, ILogger<GenericSPJsonSerializerBase> logger, IEnumerable<IResponseExceptionHandler> errorHandlers) : base(namingMappingHandler, options, logger, errorHandlers)
        {
        }

        protected override async Task WriteCurrentResultSet(Stream outputStream, DbDataReader rdr, string[] fieldNames, bool firstReadDone)
        {
            var jsonWriter = new JsonTextWriter(new StreamWriter(outputStream, options.Encoding, 1024 * 8, leaveOpen: true));

            jsonWriter.WriteStartArray();
            if (!firstReadDone)
                rdr.Read();


            do
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
            while (rdr.Read());

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync();
        }
    }



}

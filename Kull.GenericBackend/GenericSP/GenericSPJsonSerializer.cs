using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kull.GenericBackend.GenericSP
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializer :  IGenericSPSerializer
    {
        public bool SupportContentType(Microsoft.Net.Http.Headers.MediaTypeHeaderValue contentType)
        {
            return contentType.MediaType == "application/json";
        }


        private readonly Model.NamingMappingHandler namingMappingHandler;

        public GenericSPJsonSerializer(Model.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options)
        {
            this.namingMappingHandler = namingMappingHandler;
        }

        /// <summary>
        /// Prepares the header
        /// </summary>
        /// <param name="context">The http context</param>
        /// <param name="method">The Http/SP mapping</param>
        /// <param name="ent">The Entity mapping</param>
        /// <returns></returns>
        protected Task PrepareHeader(HttpContext context, Method method, Entity ent)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json; charset=utf-8";
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
        public async Task ReadResultToBody(HttpContext context, System.Data.Common.DbCommand cmd, Method method, Entity ent)
        {
            //TODO: When available use the new UTF8-Json Writer of .Net Core

            using (var rdr = await cmd.ExecuteReaderAsync())
            {
                await PrepareHeader(context, method, ent);
                using (JsonWriter jsonWriter = new JsonTextWriter(new System.IO.StreamWriter(context.Response.Body, System.Text.Encoding.UTF8)))
                {
                    string[] fieldNames = new string[rdr.FieldCount];
                    for (int i = 0; i < fieldNames.Length; i++)
                    {
                        fieldNames[i] = rdr.GetName(i);
                    }
                    fieldNames = this.namingMappingHandler.GetNames(fieldNames).ToArray();
                    jsonWriter.WriteStartArray();
                    while (rdr.Read())
                    {
                        jsonWriter.WriteStartObject();
                        for (int p = 0; p < fieldNames.Length; p++)
                        {
                            jsonWriter.WritePropertyName(fieldNames[p]);
                            object vl = rdr.GetValue(p);
                            jsonWriter.WriteValue(vl == DBNull.Value ? null : vl);
                        }
                        jsonWriter.WriteEndObject();
                    }
                    jsonWriter.WriteEndArray();
                }
            }
        }
    }
}

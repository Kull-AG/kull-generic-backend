using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
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
    public class GenericSPXmlSerializer : IGenericSPSerializer
    {
        
        public int? GetSerializerPriority(IEnumerable<Microsoft.Net.Http.Headers.MediaTypeHeaderValue> contentTypes,
            Entity entity,
            Method method)
        {
            return contentTypes.Any(contentType => contentType.MediaType == "text/html" || contentType.MediaType == "application/xhtml+xml"
                    || contentType.MediaType == "application/xml"
                    || contentType.MediaType == "text/xml") ? (int?)100: null;
        }


        private readonly Model.NamingMappingHandler namingMappingHandler;
        private readonly SPMiddlewareOptions options;
        private readonly IEnumerable<Error.IResponseExceptionHandler> errorHandlers;
        private readonly ILogger logger;

        public GenericSPXmlSerializer(Model.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
                IEnumerable<Error.IResponseExceptionHandler> errorHandlers,
                ILogger<GenericSPXmlSerializer> logger)
        {
            this.namingMappingHandler = namingMappingHandler;
            this.options = options;
            this.errorHandlers = errorHandlers;
            this.logger = logger;
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
            var isHtml = IsHtmlRequest(context);
            string contentType = "application/xml";
            if (isHtml)
            {
                contentType = "application/xhtml+xml";
            }
            context.Response.ContentType = $"{contentType}; charset={options.Encoding.BodyName}";
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
            bool html = IsHtmlRequest(context);
#if !NETSTD2
            var syncIOFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }
#endif
            try
            {
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    bool firstRead = rdr.Read();
                    await PrepareHeader(context, method, ent, 200);
                    using (var xmlWriter = new System.Xml.XmlTextWriter(context.Response.Body, options.Encoding))
                    {
                        string[] fieldNames = new string[rdr.FieldCount];
                        for (int i = 0; i < fieldNames.Length; i++)
                        {
                            fieldNames[i] = rdr.GetName(i);
                        }
                        fieldNames = this.namingMappingHandler.GetNames(fieldNames).ToArray();

                        xmlWriter.WriteStartElement("table");
                        {
                            if (html)
                            {
                                xmlWriter.WriteStartElement("thead");
                                {
                                    xmlWriter.WriteStartElement("tr");
                                    foreach (var field in fieldNames)
                                    {
                                        xmlWriter.WriteStartElement("th");
                                        xmlWriter.WriteValue(field);
                                        xmlWriter.WriteEndElement();
                                    }
                                    xmlWriter.WriteEndElement();
                                }
                                xmlWriter.WriteEndElement();//thead
                                xmlWriter.WriteStartElement("tbody");
                            }
                            while (firstRead || rdr.Read())
                            {
                                firstRead = false;
                                xmlWriter.WriteStartElement("tr");
                                for (int p = 0; p < fieldNames.Length; p++)
                                {
                                    xmlWriter.WriteStartElement(html ? "td" : fieldNames[p]);
                                    object vl = rdr.GetValue(p);
                                    xmlWriter.WriteValue(vl == DBNull.Value ? null : vl);
                                    xmlWriter.WriteEndElement();
                                }
                                xmlWriter.WriteEndElement();
                            }
                            if (html)
                            {
                                xmlWriter.WriteEndElement();
                            }
                        }
                        xmlWriter.WriteEndElement();
                    }
                }
            }
            catch (Exception err)
            {
                logger.LogWarning(err, $"Error executing {cmd.CommandText}");
                bool handled = false;
                foreach (var hand in errorHandlers)
                {
                    if (hand.CanHandle(err))
                    {
                        (int status, object? content) = hand.GetContent(err);
                        if (!context.Response.HasStarted)
                        {
                            await PrepareHeader(context, method, ent, status);
                            if (content != null)
                            {
                                var ser = new System.Xml.Serialization.XmlSerializer(content.GetType());
                                string xml;
                                using (var strW = new System.IO.StringWriter())
                                {
                                    ser.Serialize(strW, content);
                                    xml = strW.ToString();
                                }
                                await context.Response.WriteAsync(xml);
                            }
                        }
                        else
                        {
                            logger.LogError(err, $"Could not execute {cmd.CommandText} and could not handle error");
                        }
                        handled = true;
                        break;
                    }
                }
                if (!handled)
                    throw;
            }
        }

        private static bool IsHtmlRequest(HttpContext context)
        {
            return context.Request.GetTypedHeaders().Accept.Any(contentType => contentType.MediaType == "text/html" || contentType.MediaType == "application/xhtml+xml");
        }

        public bool SupportsResultType(string resultType) => resultType == "xml";

        public void ModifyResponses(OpenApiResponses responses)
        {
        }
    }
}

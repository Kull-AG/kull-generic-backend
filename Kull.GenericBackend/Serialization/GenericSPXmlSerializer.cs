using Kull.GenericBackend.Common;
using Kull.GenericBackend.Middleware;
using Kull.GenericBackend.SwaggerGeneration;
#if NET48
using Kull.MvcCompat;
using HttpContext = System.Web.HttpContextBase;
using System.Net.Http.Headers;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
#endif
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Serialization;

/// <summary>
/// Helper class for writing the result of a command to the body of the response
/// </summary>
public class GenericSPXmlSerializer : IGenericSPSerializer
{

    public int? GetSerializerPriority(IEnumerable<MediaTypeHeaderValue> contentTypes,
        Entity entity,
        Method method)
    {
        return contentTypes.Any(contentType => contentType.MediaType == "text/html" || contentType.MediaType == "application/xhtml+xml"
                || contentType.MediaType == "application/xml"
                || contentType.MediaType == "text/xml") ? (int?)100 : null;
    }


    private readonly Common.NamingMappingHandler namingMappingHandler;
    private readonly SPMiddlewareOptions options;
    private readonly IEnumerable<Error.IResponseExceptionHandler> errorHandlers;
    private readonly ILogger<GenericSPXmlSerializer> logger;
    private readonly ResponseDescriptor responseDescriptor;

    public GenericSPXmlSerializer(Common.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
            IEnumerable<Error.IResponseExceptionHandler> errorHandlers,
            ILogger<GenericSPXmlSerializer> logger,
            ResponseDescriptor responseDescriptor)
    {
        this.namingMappingHandler = namingMappingHandler;
        this.options = options;
        this.errorHandlers = errorHandlers;
        this.logger = logger;
        this.responseDescriptor = responseDescriptor;
    }

    /// <summary>
    /// Prepares the header
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="method">The Http/SP mapping</param>
    /// <param name="ent">The Entity mapping</param>
    /// <param name="statusCode">the status </param>
    /// <returns></returns>
    protected Task PrepareHeader(SerializationContext context, Method method, Entity ent, int statusCode)
    {

        var isHtml = IsHtmlRequest(context);
        string contentType = "application/xml";
        if (isHtml)
        {
            contentType = "application/xhtml+xml";
        }
        context.SetHeaders($"{contentType}; charset={options.Encoding.BodyName}", statusCode, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes the result data to the body
    /// </summary>
    /// <returns>A Task</returns>
    public async Task<Exception?> ReadResultToBody(SerializationContext serializationContext)
    {
        var method = serializationContext.Method;
        var ent = serializationContext.Entity;
        bool html = IsHtmlRequest(serializationContext);
        serializationContext.RequireSyncIO();
        try
        {
            using (var rdr = await serializationContext.ExecuteReaderAsync())
            {
                bool firstRead = rdr.Read();
                await PrepareHeader(serializationContext, method, ent, 200);

                using (var xmlWriter = new System.Xml.XmlTextWriter(serializationContext.OutputStream, options.Encoding))
                {
                    string[] fieldNames = new string[rdr.FieldCount];
                    for (int i = 0; i < fieldNames.Length; i++)
                    {
                        fieldNames[i] = rdr.GetName(i);
                    }
                    fieldNames = namingMappingHandler.GetNames(fieldNames).ToArray();

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
                                object? vl = rdr.GetValue(p);
                                xmlWriter.WriteValue(vl == DBNull.Value || vl == null ? null : vl);
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
            return null;
        }
        catch (Exception err)
        {
            bool handled = false;
            foreach (var hand in errorHandlers)
            {
                var result = hand.GetContent(err, o =>
                {
                    var ser = new System.Xml.Serialization.XmlSerializer(o.GetType());
                    string xml;
                    using (var strW = new System.IO.StringWriter())
                    {
                        ser.Serialize(strW, o);
                        xml = strW.ToString();
                    }
                    var content = new System.Net.Http.StringContent(xml);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
                    return content;
                });
                if (result != null)
                {
                    (var status, var content) = result.Value;

                    if (!serializationContext.HasResponseStarted)
                    {
                        await PrepareHeader(serializationContext, method, ent, status);
                        await serializationContext.HttpContentToResponse(content).ConfigureAwait(false);
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
            return err;
        }
    }

    private static bool IsHtmlRequest(SerializationContext context)
    {
        return context.GetAcceptHeader()?.Any(contentType => contentType.MediaType == "text/html" || contentType.MediaType == "application/xhtml+xml") == true;
    }

    public bool SupportsResultType(string resultType) => resultType == "xml";

    public virtual OpenApiResponses GetResponseType(OperationResponseContext operationResponseContext)
    {
        var schema = responseDescriptor.GetArrayOfResult(operationResponseContext.ResultTypeName);

        OpenApiResponses responses = new OpenApiResponses();
        OpenApiResponse response = new OpenApiResponse();
        response.Description = $"OK"; // Required as per spec
        response.Content.Add("application/xml", new OpenApiMediaType()
        {
            Schema = schema

        });
        responses.Add("200", response);
        return responses;
    }
}

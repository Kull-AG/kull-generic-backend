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
public class GenericSPNoneSerializer : IGenericSPSerializer
{

    public int? GetSerializerPriority(IEnumerable<MediaTypeHeaderValue> contentTypes,
        Entity entity,
        Method method)
    {
        return null;
    }


    private readonly Common.NamingMappingHandler namingMappingHandler;
    private readonly SPMiddlewareOptions options;
    Error.JsonErrorHandler jsonErrorHandler;
    private readonly ILogger<GenericSPXmlSerializer> logger;
    private readonly ResponseDescriptor responseDescriptor;

    public GenericSPNoneSerializer(Common.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
            Error.JsonErrorHandler jsonErrorHandler,
            ILogger<GenericSPXmlSerializer> logger,
            ResponseDescriptor responseDescriptor)
    {
        this.namingMappingHandler = namingMappingHandler;
        this.options = options;
        this.jsonErrorHandler = jsonErrorHandler;
        this.logger = logger;
        this.responseDescriptor = responseDescriptor;
    }

    /// <summary>
    /// Prepares the header
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="method">The Http/SP mapping</param>
    /// <param name="ent">The Entity mapping</param>
    /// <param name="statusCode">The HTTP status</param>
    /// <returns></returns>
    protected Task PrepareHeader(SerializationContext context, Method method, Entity ent, int statusCode)
    {
        context.SetHeaders("text/plain", statusCode, true);
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
        try
        {
            await serializationContext.ExecuteNonQueryAsync();
            await PrepareHeader(serializationContext, method, ent, 200);
            return null;
        }
        catch (Exception err)
        {
            var handled = await jsonErrorHandler.SerializeErrorAsJson(err, serializationContext);

            if (!handled)
                throw;
            return err;
        }
    }

    public bool SupportsResultType(string resultType) => resultType == "none";

    public virtual OpenApiResponses GetResponseType(OperationResponseContext operationResponseContext)
    {
        OpenApiResponses responses = new OpenApiResponses();
        OpenApiResponse response = new OpenApiResponse();
        response.Description = $"OK"; // Required as per spec
        responses.Add("200", response);
        return responses;
    }
}

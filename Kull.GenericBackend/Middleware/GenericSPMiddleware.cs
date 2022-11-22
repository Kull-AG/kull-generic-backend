using System;
using System.Collections.Generic;
using System.Linq;
#if NET48
using HttpContext = System.Web.HttpContextBase;
using System.Web.Routing;
using Kull.MvcCompat;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
#endif
using Kull.Data;
using System.Threading.Tasks;
using System.Data.Common;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.Serialization;
using Kull.GenericBackend.Parameters;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Filter;
using System.Net.Http.Headers;
using System.Net.Mime;
using Kull.GenericBackend.Execution;

namespace Kull.GenericBackend.Middleware;

/// <summary>
/// The middleware doing the actual execution
/// </summary>
public class GenericSPMiddleware : IGenericSPMiddleware
{
    private readonly ILogger<GenericSPMiddleware> logger;
    private readonly SerializerResolver serializerResolver;
    private readonly SPMiddlewareOptions sPMiddlewareOptions;
    private readonly DbConnection dbConnection;
    private readonly IEnumerable<IRequestInterceptor> requestInterceptors;
    private readonly CommandPreparation commandPreparation;
#if NET48
    private readonly IPolicyResolver policyResolver;
#endif
    private readonly IEnumerable<Filter.RequestLogger> requestLoggers;

    public GenericSPMiddleware(
        ILogger<GenericSPMiddleware> logger,
        SerializerResolver serializerResolver,
        SPMiddlewareOptions sPMiddlewareOptions,
        DbConnection dbConnection,
        IEnumerable<Filter.IRequestInterceptor> requestInterceptors,
        IEnumerable<Filter.RequestLogger> requestLoggers,
        CommandPreparation commandPreparation
#if NET48
        , IPolicyResolver policyResolver
#endif
        )
    {
        this.logger = logger;
        this.serializerResolver = serializerResolver;
        this.sPMiddlewareOptions = sPMiddlewareOptions;
        this.dbConnection = dbConnection;
        this.requestInterceptors = requestInterceptors;
        this.commandPreparation = commandPreparation;
#if NET48
        this.policyResolver = policyResolver;
#endif
        this.requestLoggers = requestLoggers;
    }

    public Task HandleRequest(HttpContext context, Entity ent)
    {

#if NET48
        var method = ent.GetMethod(context.Request.HttpMethod);
#else
        var method = ent.GetMethod(context.Request.Method);
#endif
        foreach (var interceptor in this.requestInterceptors)
        {
            var shouldIntercept = interceptor.OnBeforeRequest(context, new RequestInterceptorContext(
                ent, method, this.dbConnection));
            if (shouldIntercept != null)
            {
                context.Response.StatusCode = shouldIntercept.Value.statusCode;
                return HttpHandlingUtils.HttpContentToResponse(shouldIntercept.Value.responseContent, context.Response);
            }
        }
#if NET48
        var accept = (context.Request.Headers["Accept"] ?? "").Split(',').Select(ac => MediaTypeHeaderValue.Parse(ac)).ToList();
#else
        var accept = context.Request.GetTypedHeaders().Accept;
#endif
        IGenericSPSerializer? serializer = serializerResolver.GetSerialializerOrNull(accept,
            ent, method);
        if (serializer == null)
        {
            context.Response.StatusCode = 415;
            return Task.CompletedTask;
        }
#if NET48 // Done by Framework in .Net Core
        string[] policies = (method.Policies ?? sPMiddlewareOptions.Policies).ToArray();
        if (policies.Length > 0)
        {
            if (!policies.All(p => policyResolver.UserIsInPolicy(context, p)))
            {
                return Handle40x(context, ent, method, serializer, RequestLogger.RequestValidationFailedReason.PolicyFailedNetFx);
            }
        }
#endif

        if (this.sPMiddlewareOptions.RequireAuthenticated && context.User?.Identity?.IsAuthenticated != true)
        {
            return Handle40x(context, ent, method, serializer, RequestLogger.RequestValidationFailedReason.AuthenticationNotGiven);
        }
#if NET48
        if (context.Request.HttpMethod.ToUpper() == "GET")
#else
        if (context.Request.Method.ToUpper() == "GET")
#endif
        {
            return HandleGetRequest(context, ent, serializer);
        }
        return HandleBodyRequest(context, method, ent, serializer);
    }

    protected async Task Handle40x(HttpContext context, Entity ent, Method method, IGenericSPSerializer serializer, RequestLogger.RequestValidationFailedReason reason )
    {
        context.Response.StatusCode = reason == RequestLogger.RequestValidationFailedReason.AuthenticationNotGiven ? 401: 403;
        foreach (var log in requestLoggers)
        {
            log.OnRequestValidationFailed(context, new RequestLogger.RequestValidationFailedInfo(reason));
        }
        var objReader = new Kull.Data.DataReader.ObjectDataReader(new IReadOnlyDictionary<string, object?>[]{ new Dictionary<string, object?>()
                {
                    {"Error", reason == RequestLogger.RequestValidationFailedReason.AuthenticationNotGiven ? "You are Unauthenticated" : "You are not permitted" }
                } });
        var sercontext = new SerializationContextResult(objReader, context, method, ent, 401);
        await serializer.ReadResultToBody(sercontext);

    }

    protected async Task HandleGetRequest(HttpContext context, Entity ent, IGenericSPSerializer serializer)
    {
        var method = ent.Methods[OperationType.Get];
        var request = context.Request;

        Dictionary<string, object> queryParameters;
#if NET48
        queryParameters = request.QueryString.AllKeys.ToDictionary(k => k,
            k => (object)request.QueryString.Get(k), StringComparer.CurrentCultureIgnoreCase);
#else
        if (request.QueryString.HasValue && request.QueryString.Value != null)
        {
            var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.QueryString.Value);
            queryParameters = queryDictionary
                    .ToDictionary(kv => kv.Key,
                        kv => string.Join(",", kv.Value) as object,
                        StringComparer.CurrentCultureIgnoreCase);

        }
        else
        {
            queryParameters = new Dictionary<string, object>();
        }
#endif
        var (cmd, hasOutputParameters) = await commandPreparation.GetCommandWithParameters(context, null, dbConnection, ent, method, queryParameters);
        var start = DateTime.UtcNow;
        foreach (var log in requestLoggers)
        {
            await log.OnRequestStartAsync(context, new RequestLogger.RequestStartInfo(cmd));
        }
        var excep = await serializer.ReadResultToBody(new SerializationContextCmd(cmd, context, method, ent, hasOutputParameters));
        foreach (var log in requestLoggers)
        {
            await log.OnRequestEndAsync(context, new RequestLogger.RequestEndInfo(cmd, start, excep));
        }
    }



    private bool HasApplicationFormContentType(MediaTypeHeaderValue contentType)
    {
        // Content-Type: application/x-www-form-urlencoded; charset=utf-8
        return contentType != null && contentType.MediaType != null && contentType.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasMultipartFormContentType(MediaTypeHeaderValue contentType)
    {
        // Content-Type: multipart/form-data; boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq
        return contentType != null && contentType.MediaType != null && contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }


    protected async Task HandleBodyRequest(HttpContext context, Method method, Entity ent, IGenericSPSerializer serializer)
    {
        var request = context.Request;
        Dictionary<string, object> parameterObject;
#if NET48
        var cntType = MediaTypeHeaderValue.Parse(request.ContentType);
        bool hasFormContentType = HasMultipartFormContentType(cntType) || HasApplicationFormContentType(cntType);
#else
        bool hasFormContentType = request.HasFormContentType;
#endif
        if (hasFormContentType)
        {
            parameterObject = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
#if NETFX
            foreach (var key in request.Form.AllKeys)
            {
                parameterObject.Add(key, request.Form[key]);
            }
            foreach (var key in request.Files.AllKeys)
            {
                parameterObject.Add(key, request.Files[key]);
            }
#else
            foreach (var item in request.Form)
            {
                // In case the items are JSON, we should make sure it's still valid json after grouping by key
                parameterObject.Add(item.Key, 
                    item.Value.Count ==1 ? item.Value[0] 
                    : "[" + string.Join(",", item.Value) + "]");
            }
            foreach (var file in request.Form.Files)
            {
                parameterObject.Add(file.Name, file);
            }
#endif
        }
        else
        {
#if NETFX
            var streamReader = new System.IO.StreamReader(request.InputStream);
#else
            var streamReader = new System.IO.StreamReader(request.Body);
#endif
            string json = await streamReader.ReadToEndAsync();
#if NEWTONSOFTJSON
            parameterObject = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
            Newtonsoft.Json.JsonConvert.PopulateObject(json, parameterObject);
#else
            var retValue = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            parameterObject = retValue!.ToDictionary(k => k.Key, v => Config.DictionaryHelper.ConvertToDeepIDictionary(v.Value, StringComparer.CurrentCultureIgnoreCase), StringComparer.CurrentCultureIgnoreCase)!;
#endif
        }
        var (cmd, hasOutputParameters) = await commandPreparation.GetCommandWithParameters(context, null, dbConnection, ent, method, parameterObject);
        var start = DateTime.UtcNow;
        foreach (var log in requestLoggers)
        {
            await log.OnRequestStartAsync(context, new RequestLogger.RequestStartInfo(cmd));
        }
        var excep = await serializer.ReadResultToBody(new SerializationContextCmd(cmd, context, method, ent,hasOutputParameters));
        foreach (var log in requestLoggers)
        {
            await log.OnRequestEndAsync(context, new RequestLogger.RequestEndInfo(cmd, start, excep));
        }
    }




}

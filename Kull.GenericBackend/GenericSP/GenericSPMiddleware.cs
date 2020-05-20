using System;
using System.Collections.Generic;
using System.Linq;
#if NET47
using System.Web;
using System.Web.Routing;
using Kull.MvcCompat;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
#endif
using Kull.Data;
using Newtonsoft.Json;
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

namespace Kull.GenericBackend.GenericSP
{

    /// <summary>
    /// The middleware doing the actual execution
    /// </summary>
    public class GenericSPMiddleware : IGenericSPMiddleware
    {
        private readonly SqlHelper sqlHelper;
        private readonly ParameterProvider parameterProvider;

        private readonly ILogger<GenericSPMiddleware> logger;
        private readonly SerializerResolver serializerResolver;
        private readonly SPMiddlewareOptions sPMiddlewareOptions;
        private readonly SPParametersProvider sPParametersProvider;
        private readonly DbConnection dbConnection;
        private readonly IEnumerable<IRequestInterceptor> requestInterceptors;

        public GenericSPMiddleware(
            ParameterProvider parameterProvider,
            SqlHelper sqlHelper,
            ILogger<GenericSPMiddleware> logger,
            SerializerResolver serializerResolver,
            SPParametersProvider sPParametersProvider,
            SPMiddlewareOptions sPMiddlewareOptions,
            DbConnection dbConnection,
            IEnumerable<Filter.IRequestInterceptor> requestInterceptors)
        {
            this.logger = logger;
            this.serializerResolver = serializerResolver;
            this.sPMiddlewareOptions = sPMiddlewareOptions;
            this.dbConnection = dbConnection;
            this.requestInterceptors = requestInterceptors;
            this.parameterProvider = parameterProvider;
            this.sqlHelper = sqlHelper;
            this.sPParametersProvider = sPParametersProvider;
        }

        public Task HandleRequest(HttpContext context, Entity ent)
        {
#if NET47
            var method = ent.GetMethod(context.Request.HttpMethod);
#else
            var method = ent.GetMethod(context.Request.Method);
#endif
            foreach (var interceptor in this.requestInterceptors)
            {
                var shouldIntercept = interceptor.OnBeforeRequest(context, new RequestInterceptorContext(
                    ent, method, this.dbConnection));
                if(shouldIntercept != null)
                {
                    context.Response.StatusCode = shouldIntercept.Value.statusCode;
                    return HttpHandlingUtils.HttpContentToResponse(shouldIntercept.Value.responseContent, context.Response);
                }
            }
#if NET47 
            var accept = new MediaTypeHeaderValue[] { MediaTypeHeaderValue.Parse(context.Request.Headers["Accept"]) }.ToList();
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
            if (this.sPMiddlewareOptions.RequireAuthenticated && context.User?.Identity == null)
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
#if NET47
            if (context.Request.HttpMethod.ToUpper() == "GET")
#else
            if (context.Request.Method.ToUpper() == "GET")
#endif
            {
                return HandleGetRequest(context, ent, serializer);
            }
            return HandleBodyRequest(context, method, ent, serializer);
        }

        protected async Task HandleGetRequest(HttpContext context, Entity ent, IGenericSPSerializer serializer)
        {
            var method = ent.Methods[OperationType.Get];
            var request = context.Request;

            Dictionary<string, object> queryParameters;
#if NET47
            queryParameters = request.QueryString.AllKeys.ToDictionary(k => k,
                k => (object)request.QueryString.Get(k), StringComparer.CurrentCultureIgnoreCase);
#else
            if (request.QueryString.HasValue)
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
            var cmd = GetCommandWithParameters(context, dbConnection, ent, method, queryParameters);

            await serializer.ReadResultToBody(new SerializationContext(cmd, context, method, ent));

        }



        private bool HasApplicationFormContentType(MediaTypeHeaderValue contentType)
        {
            // Content-Type: application/x-www-form-urlencoded; charset=utf-8
            return contentType != null && contentType.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        private bool HasMultipartFormContentType(MediaTypeHeaderValue contentType)
        {
            // Content-Type: multipart/form-data; boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq
            return contentType != null && contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
        }


        protected async Task HandleBodyRequest(HttpContext context, Method method, Entity ent, IGenericSPSerializer serializer)
        {
            var request = context.Request;
            Dictionary<string, object> parameterObject;
#if NET47
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
                    parameterObject.Add(item.Key, string.Join(",", item.Value));
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
                parameterObject = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
                JsonConvert.PopulateObject(json, parameterObject);
            }
            var cmd = GetCommandWithParameters(context, dbConnection, ent, method, parameterObject);
            await serializer.ReadResultToBody(new SerializationContext(cmd, context, method, ent));

        }

        protected DbCommand GetCommandWithParameters(HttpContext context,
                DbConnection con,
            Entity ent,
                Method method, Dictionary<string, object> parameterOfUser)
        {
            if (con == null) throw new ArgumentNullException(nameof(con));
            if (ent == null) throw new ArgumentNullException(nameof(ent));
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (parameterOfUser == null) { parameterOfUser = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase); }
            var cmd = con.AssureOpen().CreateSPCommand(method.SP);
            if(method.CommandTimeout != null)
            {
                cmd.CommandTimeout = method.CommandTimeout.Value;
            }
            var parameters = parameterProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method, false));
            SPParameter[]? sPParameters = null;
            foreach (var apiPrm in parameters)
            {
                var prm = apiPrm.WebApiName == null ? parameterOfUser /* make it possible to use some logic */
                        :
                        ent.ContainsPathParameter(apiPrm.WebApiName) ?
                        context.GetRouteValue(apiPrm.WebApiName) :
                        parameterOfUser.FirstOrDefault(p => p.Key.Equals(apiPrm.WebApiName,
                            StringComparison.CurrentCultureIgnoreCase)).Value;
                if (apiPrm.SqlName == null)
                    continue;
                object? value = apiPrm.GetValue(context, prm);

                if (value is System.Data.DataTable dt)
                {

                    var cmdPrm = cmd.CreateParameter();
                    cmdPrm.ParameterName = "@" + apiPrm.SqlName;
                    cmdPrm.Value = value;
                    if (cmdPrm.GetType().FullName == "System.Data.SqlClient.SqlParameter" ||
                        cmdPrm.GetType().FullName == "Microsoft.Data.SqlClient.SqlParameter")
                    {

                        // Reflection set SqlDbType in order to avoid 
                        // referecnting the deprecated SqlClient Nuget Package or the too new Microsoft SqlClient package

                        // see https://devblogs.microsoft.com/dotnet/introducing-the-new-microsoftdatasqlclient/

                        // cmdPrm.SqlDbType = System.Data.SqlDbType.Structured;
                        cmdPrm.GetType().GetProperty("SqlDbType", System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.SetProperty)!
                            .SetValue(cmdPrm, System.Data.SqlDbType.Structured);
                    }
                    cmd.Parameters.Add(cmdPrm);
                }
                else if (value as string == "")
                {
                    sPParameters = sPParameters ?? sPParametersProvider.GetSPParameters(method.SP, con);
                    var spPrm = sPParameters.First(f => f.SqlName == apiPrm.SqlName);
                    if (spPrm.DbType.NetType == typeof(System.DateTime)
                        || spPrm.DbType.JsType == "number"
                         || spPrm.DbType.JsType == "integer")
                    {
                        cmd.AddCommandParameter(apiPrm.SqlName, DBNull.Value);
                    }
                }
                else
                {
                    cmd.AddCommandParameter(apiPrm.SqlName, value ?? System.DBNull.Value);
                }
            }
            return cmd;
        }



    }
}

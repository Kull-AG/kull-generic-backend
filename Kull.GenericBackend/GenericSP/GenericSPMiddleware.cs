using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Kull.Data;
using Newtonsoft.Json;
using Kull.GenericBackend.Model;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Kull.GenericBackend.SwaggerGeneration;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.Serialization;
using Kull.GenericBackend.Parameters;
using Microsoft.AspNetCore.Authorization;
using System.Reflection.Emit;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Filter;
using System.Net.Http;

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
        private readonly IAuthorizationPolicyProvider policyProvider;
        private readonly IEnumerable<IRequestInterceptor> requestInterceptors;

        public GenericSPMiddleware(
            ParameterProvider parameterProvider,
            SqlHelper sqlHelper,
            ILogger<GenericSPMiddleware> logger,
            SerializerResolver serializerResolver,
            SPParametersProvider sPParametersProvider,
            SPMiddlewareOptions sPMiddlewareOptions,
            DbConnection dbConnection,
            IAuthorizationPolicyProvider policyProvider,
            IEnumerable<Filter.IRequestInterceptor> requestInterceptors)
        {
            this.logger = logger;
            this.serializerResolver = serializerResolver;
            this.sPMiddlewareOptions = sPMiddlewareOptions;
            this.dbConnection = dbConnection;
            this.policyProvider = policyProvider;
            this.requestInterceptors = requestInterceptors;
            this.parameterProvider = parameterProvider;
            this.sqlHelper = sqlHelper;
            this.sPParametersProvider = sPParametersProvider;
        }

        public Task HandleRequest(HttpContext context, Entity ent)
        {
            var method = ent.GetMethod(context.Request.Method);
            foreach(var interceptor in this.requestInterceptors)
            {
                var shouldIntercept = interceptor.OnBeforeRequest(context, new RequestInterceptorContext(
                    ent, method, this.dbConnection));
                if(shouldIntercept != null)
                {
                    return WriteResponse(context, shouldIntercept.Value.statusCode, shouldIntercept.Value.responseContent);
                }
            }
            IGenericSPSerializer? serializer = serializerResolver.GetSerialializerOrNull(context.Request.GetTypedHeaders().Accept,
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

            if (context.Request.Method.ToUpper() == "GET")
            {
                return HandleGetRequest(context, ent, serializer);
            }
            return HandleBodyRequest(context, method, ent, serializer);
        }

        protected async Task WriteResponse(HttpContext context, int statusCode, HttpContent responseContent)
        {
            var response = context.Response;
            response.StatusCode = statusCode;
            foreach (var header in responseContent.Headers)
                response.Headers.Add(header.Key, new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray()));
            await responseContent.CopyToAsync(response.Body).ConfigureAwait(false);
        }

        protected async Task HandleGetRequest(HttpContext context, Entity ent, IGenericSPSerializer serializer)
        {
            var method = ent.Methods[OperationType.Get];
            var request = context.Request;

            Dictionary<string, object> queryParameters;

            if (request.QueryString.HasValue)
            {
                var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.QueryString.Value);
                queryParameters = queryDictionary
                        .ToDictionary(kv => kv.Key,
                            kv => string.Join(",", kv.Value) as object);

            }
            else
            {
                queryParameters = new Dictionary<string, object>();
            }
            var cmd = GetCommandWithParameters(context, dbConnection, ent, method, queryParameters);

            await serializer.ReadResultToBody(new SerializationContext(cmd, context, method, ent));

        }


        protected async Task HandleBodyRequest(HttpContext context, Method method, Entity ent, IGenericSPSerializer serializer)
        {
            var request = context.Request;
            Dictionary<string, object> parameterObject;
            if (request.HasFormContentType)
            {
                parameterObject = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
                foreach (var item in request.Form)
                {
                    parameterObject.Add(item.Key, string.Join(",", item.Value));
                }
                foreach (var file in request.Form.Files)
                {
                    parameterObject.Add(file.Name, file);
                }
            }
            else
            {
                var streamReader = new System.IO.StreamReader(request.Body);
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

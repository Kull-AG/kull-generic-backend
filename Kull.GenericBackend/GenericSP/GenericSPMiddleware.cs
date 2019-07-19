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

namespace Kull.GenericBackend.GenericSP
{

    /// <summary>
    /// The middleware doing the actual execution
    /// </summary>
    public class GenericSPMiddleware : IGenericSPMiddleware
    {
        private readonly SystemParameters systemParameters;
        private readonly SqlHelper sqlHelper;
        private readonly SPParametersProvider sPParametersProvider;

        private readonly ILogger<GenericSPMiddleware> logger;
        private readonly IEnumerable<IGenericSPSerializer> serializers;
        private readonly SPMiddlewareOptions sPMiddlewareOptions;
        private readonly DbConnection dbConnection;

        public GenericSPMiddleware(
            SPParametersProvider sPParametersProvider,
                SystemParameters systemParameters,
                SqlHelper sqlHelper,
                ILogger<GenericSPMiddleware> logger,
                IEnumerable<IGenericSPSerializer> serializers,
                SPMiddlewareOptions sPMiddlewareOptions,
                DbConnection dbConnection)
        {
            this.logger = logger;
            this.serializers = serializers;
            this.sPMiddlewareOptions = sPMiddlewareOptions;
            this.dbConnection = dbConnection;
            this.sPParametersProvider = sPParametersProvider;
            this.systemParameters = systemParameters;
            this.sqlHelper = sqlHelper;
        }

        public Task HandleRequest(HttpContext context, Entity ent)
        {
            IGenericSPSerializer serializer = null;
            var accept = context.Request.GetTypedHeaders().Accept ??
                   new List<Microsoft.Net.Http.Headers.MediaTypeHeaderValue>() {
                     new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/json")
                     };

            foreach (var ser in serializers)
            {
                if (accept.Any(a => ser.SupportContentType(a)))
                {
                    serializer = ser;
                    break;
                }
            }
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
            if (context.Request.Method == "GET")
            {
                return HandleGetRequest(context, ent, serializer);
            }
            var method = ent.Methods[context.Request.Method];
            return HandleBodyRequest(context, method, ent, serializer);
        }
        private async Task HandleGetRequest(HttpContext context, Entity ent, IGenericSPSerializer serializer)
        {
            var method = ent.Methods["Get"];
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

            await serializer.ReadResultToBody(context, cmd, method, ent);

        }

        private XElement ToXml(IDictionary<string, object> input)
        {
            return new XElement("el",
                input.Keys.Select(k => new XAttribute(k, input[k])));
        }


        private XElement ToXml(IEnumerable<IDictionary<string, object>> input)
        {
            return new XElement("root",
                input.Select(s => ToXml(s)));
        }


        private async Task HandleBodyRequest(HttpContext context, Method method, Entity ent, IGenericSPSerializer serializer)
        {
            var request = context.Request;

            var streamReader = new System.IO.StreamReader(request.Body);
            string json = streamReader.ReadToEnd();
            var js = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            var cmd = GetCommandWithParameters(context, dbConnection, ent, method, js);
            await serializer.ReadResultToBody(context, cmd, method, ent);

        }

        private DbCommand GetCommandWithParameters(HttpContext context,
                DbConnection con,
            Entity ent,
                Method method, Dictionary<string, object> parameterOfUser)
        {
            if (con == null) throw new ArgumentNullException(nameof(con));
            if (ent == null) throw new ArgumentNullException(nameof(ent));
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (parameterOfUser == null) { parameterOfUser = new Dictionary<string, object>(); }
            var cmd = con.AssureOpen().CreateSP(method.SP);
            var parameters = method.GetParameters(con, sPParametersProvider);
            foreach (var prm in parameterOfUser)
            {
                var spPrm = parameters.FirstOrDefault(p => p.WebApiName != null && p.WebApiName.Equals(prm.Key, StringComparison.CurrentCultureIgnoreCase));
                if (spPrm == null || ent.ContainsPathParameter(spPrm.WebApiName))
                {
                    logger.LogWarning("Extra body parameter {0}", prm.Key);
                }
                else if (prm.Value is IDictionary<string, object> obj)
                {
                    if (spPrm.SqlName.EndsWith("Xml"))
                    {
                        cmd.AddCommandParameter(spPrm.SqlName, ToXml(obj));
                    }
                    else if (spPrm.UserDefinedType != null)
                    {
                        AddTableParameter(cmd, spPrm, new IDictionary<string, object>[] { obj });
                    }
                    else
                    {
                        cmd.AddCommandParameter(spPrm.SqlName, JsonConvert.SerializeObject(obj));
                    }
                }
                else if (prm.Value is IEnumerable<Dictionary<string, object>> objAr)
                {
                    if (spPrm.SqlName.EndsWith("Xml"))
                    {
                        cmd.AddCommandParameter(spPrm.SqlName, ToXml(objAr));
                    }
                    else if (spPrm.UserDefinedType != null)
                    {
                        AddTableParameter(cmd, spPrm, objAr);
                    }
                    else
                    {
                        cmd.AddCommandParameter(spPrm.SqlName, JsonConvert.SerializeObject(objAr));
                    }
                }
                else if (prm.Value is Newtonsoft.Json.Linq.JArray ar)
                {
                    if (spPrm.UserDefinedType != null)
                    {
                        var jobjAr = ar.Cast<Newtonsoft.Json.Linq.JObject>()
                            .Select(oo => oo.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToObject<object>())
                                ).ToArray();
                        AddTableParameter(cmd, spPrm, jobjAr);
                    }
                    else
                    {
                        cmd.AddCommandParameter(spPrm.SqlName, JsonConvert.SerializeObject(prm.Value));
                    }
                }
                else if (prm.Value is Newtonsoft.Json.Linq.JObject obj2)
                {
                    if (spPrm.UserDefinedType != null)
                    {
                        var jar_ob = new IDictionary<string, object>[]
                        {
                            obj2.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToObject<object>())

                        };
                        AddTableParameter(cmd, spPrm, jar_ob);
                    }
                    else
                    {
                        cmd.AddCommandParameter(spPrm.SqlName, JsonConvert.SerializeObject(prm.Value));
                    }
                }
                else
                {
                    cmd.AddCommandParameter(spPrm.SqlName, prm.Value);
                }
            }
            foreach (var item in parameters.Where(p => p.WebApiName != null && ent.ContainsPathParameter(p.WebApiName)))
            {
                var value = context.GetRouteValue(item.WebApiName);
                cmd.AddCommandParameter(item.SqlName, value);
            }
            foreach (var prm in systemParameters.GetSystemParameters())
            {
                if (parameters.Select(s => s.SqlName).Contains(prm, StringComparer.CurrentCultureIgnoreCase))
                {
                    cmd.AddCommandParameter(prm, systemParameters.GetValue(prm, context));
                }
            }
            return cmd;
        }

        private void AddTableParameter(DbCommand cmd, SPParameter spPrm, IEnumerable<IDictionary<string, object>> rowData)
        {
            System.Data.DataTable dt;

            ISqlMappedData[] cols;


            cols = sqlHelper.GetTableTypeFields(dbConnection, spPrm.UserDefinedType);
            dt = new System.Data.DataTable();
            foreach (var col in cols)
            {
                dt.Columns.Add(col.SqlName, col.DbType.NetType);
            }


            foreach (var row in rowData)
            {
                object[] values = new object[dt.Columns.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    var colWebApiName = cols[i].WebApiName;
                    values[i] = row.ContainsKey(colWebApiName) ?
                        row[colWebApiName] : null;
                }
                dt.Rows.Add(values);
            }
            var cmdPrm = cmd.CreateParameter();
            if (cmdPrm.GetType().FullName == "System.Data.SqlClient.SqlParameter" ||
                cmdPrm.GetType().FullName == "Microsoft.Data.SqlClient.SqlParameter")
            {

                // Reflection set SqlDbType in order to avoid 
                // referecnting the deprecated SqlClient Nuget Package or the too new Microsoft SqlClient package

                // see https://devblogs.microsoft.com/dotnet/introducing-the-new-microsoftdatasqlclient/

                // cmdPrm.SqlDbType = System.Data.SqlDbType.Structured;
                cmdPrm.GetType().GetProperty("SqlDbType", System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.SetProperty)
                    .SetValue(cmdPrm, System.Data.SqlDbType.Structured);
            }
            cmdPrm.Value = dt;
            cmdPrm.ParameterName = "@" + spPrm.SqlName;
            cmd.Parameters.Add(cmdPrm);
        }


    }
}

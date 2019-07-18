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
    public class SPMiddlewareOptions
    {
        public string Prefix { get; set; } = "/api/";
        public string ConnectionStringKey { get; set; } = "Default";

        public string ConnectionString { get; set; }
    }

    /// <summary>
    /// The middleware doing the actual execution
    /// </summary>
    public class GenericSPMiddleware
    {
        private readonly IReadOnlyCollection<Entity> entities;
        private readonly SystemParameters systemParameters;
        private readonly SqlHelper sqlHelper;
        private readonly NamingMappingHandler namingMappingHandler;
        private readonly SPParametersProvider sPParametersProvider;

        private readonly ILogger<GenericSPMiddleware> logger;
        private readonly GenericSPSerializer genericSPSerializer;
        private SPMiddlewareOptions options;

        public GenericSPMiddleware(IConfiguration conf,
            SPParametersProvider sPParametersProvider,
                SystemParameters systemParameters,
                SqlHelper sqlHelper,
                NamingMappingHandler namingMappingHandler,
                ILogger<GenericSPMiddleware> logger,
                GenericSPSerializer genericSPSerializer)
        {
            this.logger = logger;
            this.genericSPSerializer = genericSPSerializer;
            this.sPParametersProvider = sPParametersProvider;
            this.systemParameters = systemParameters;
            this.sqlHelper = sqlHelper;
            this.namingMappingHandler = namingMappingHandler;
            var ent = conf.GetSection("Entities");
            entities = ent.GetChildren()
                   .Select(s => Entity.GetFromSection(s)).ToList();
        }

        /// <summary>
        /// Registers the actual middlware
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="routeBuilder">The routebuilder</param>
        protected internal void RegisterMiddleware(SPMiddlewareOptions options,
                IRouteBuilder routeBuilder)
        {
            this.options = options;
            foreach (var ent in entities)
            {
                if (ent.Methods.ContainsKey("GET"))
                {

                    routeBuilder.MapGet(GetUrlForMvcRouting(ent), context =>
                    {
                        return HandleGetRequest(context, ent);
                    });
                }
                if (ent.Methods.ContainsKey("PUT"))
                {
                    routeBuilder.MapPut(GetUrlForMvcRouting(ent), context =>
                    {
                        return HandleBodyRequest(context, ent.Methods["PUT"], ent);
                    });
                }
                if (ent.Methods.ContainsKey("POST"))
                {
                    routeBuilder.MapPost(GetUrlForMvcRouting(ent), context =>
                    {
                        return HandleBodyRequest(context, ent.Methods["POST"], ent);
                    });
                }
                if (ent.Methods.ContainsKey("DELETE"))
                {
                    routeBuilder.MapDelete(GetUrlForMvcRouting(ent), context =>
                    {
                        return HandleBodyRequest(context, ent.Methods["DELETE"], ent);
                    });
                }
            }
        }

        private string GetUrlForMvcRouting(Entity ent)
        {
            var url = ent.GetUrl(options.Prefix, true);
            if (url.StartsWith("/"))
                return url.Substring(1);
            return url;
        }

        protected DbConnection GetDbConnection()
        {
            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                return DatabaseUtils.GetConnectionFromEFString(options.ConnectionString, true);
            }
            return DatabaseUtils.GetConnectionFromConfig(options.ConnectionStringKey);
        }

        private async Task HandleGetRequest(HttpContext context, Entity ent)
        {
            var method = ent.Methods["Get"];
            var request = context.Request;
            using (var con = GetDbConnection())
            {
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
                var cmd = GetCommandWithParameters(context, con, ent, method, queryParameters);
                await genericSPSerializer.ReadResultToBody(context, cmd, method, ent);
            }
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


        private async Task HandleBodyRequest(HttpContext context, Method method, Entity ent)
        {
            var request = context.Request;
            using (var con = GetDbConnection())
            {
                var streamReader = new System.IO.StreamReader(request.Body);
                string json = streamReader.ReadToEnd();
                var js = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                var cmd = GetCommandWithParameters(context, con, ent, method, js);
                await genericSPSerializer.ReadResultToBody(context, cmd, method, ent);
            }
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
            using (var con = GetDbConnection())
            {
                cols = sqlHelper.GetTableTypeFields(con, spPrm.UserDefinedType);
                dt = new System.Data.DataTable();
                foreach (var col in cols)
                {
                    dt.Columns.Add(col.SqlName, col.DbType.NetType);
                }
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
            if (cmdPrm.GetType().FullName == "System.Data.SqlClient.SqlParameter"||
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

using Kull.GenericBackend.Common;
#if NET47
using HttpContext = System.Web.HttpContextBase;
using System.Web.Routing;
using Kull.MvcCompat;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
#endif
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Kull.Data;
using System.Linq;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Parameters;
using System.Data;

namespace Kull.GenericBackend.Execution
{
    public class CommandPreparation
    {
        private readonly ParameterProvider parameterProvider;
        private readonly SPParametersProvider sPParametersProvider;

        public CommandPreparation(IServiceProvider serviceProvider) // Use of IServiceProvider to not break inheritance
        {
            this.parameterProvider = (ParameterProvider)serviceProvider.GetService(typeof(ParameterProvider));
            this.sPParametersProvider = (SPParametersProvider)serviceProvider.GetService(typeof(SPParametersProvider));
        }

        /// <summary>
        /// Gets the command with parameters
        /// </summary>
        /// <param name="context">The http context or null if not in an http context</param>
        /// <param name="getRouteValue">If context is null, you need to provider route parameters with this Func</param>
        /// <param name="con">The db connection</param>
        /// <param name="ent">The entity</param>
        /// <param name="method">The method</param>
        /// <param name="parameterOfUser">The parameters as provided from user</param>
        /// <returns>Returns a DbCommand Object, NOT executed</returns>
        public virtual DbCommand GetCommandWithParameters(HttpContext? context,
                Func<string, object>? getRouteValue,
                DbConnection con,
                Entity ent,
                Method method,
                Dictionary<string, object> parameterOfUser)
        {
            if (con == null) throw new ArgumentNullException(nameof(con));
            if (ent == null) throw new ArgumentNullException(nameof(ent));
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (parameterOfUser == null) { parameterOfUser = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase); }
            if (getRouteValue == null)
            {
                if (context == null)
                    throw new ArgumentNullException(nameof(getRouteValue));
                getRouteValue = (s) => context.GetRouteValue(s);
            }
            var cmd = con.AssureOpen().CreateSPCommand(method.SP);
            if (method.CommandTimeout != null)
            {
                cmd.CommandTimeout = method.CommandTimeout.Value;
            }
            var (inputParameters, outputParameters) = parameterProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method, false), con);
            SPParameter[]? sPParameters = null;
            foreach (var apiPrm in inputParameters)
            {
                var prm = apiPrm.WebApiName == null ? parameterOfUser /* make it possible to use some logic */
                        :
                        ent.ContainsPathParameter(apiPrm.WebApiName) ?
                        getRouteValue(apiPrm.WebApiName) :
                        parameterOfUser.FirstOrDefault(p => p.Key.Equals(apiPrm.WebApiName,
                            StringComparison.CurrentCultureIgnoreCase)).Value;
                if (apiPrm.SqlName == null)
                    continue;
                bool hasValue = !apiPrm.RequiresUserProvidedValue
                    || (apiPrm.WebApiName != null && ent.ContainsPathParameter(apiPrm.WebApiName))
                    || parameterOfUser.Any(p => p.Key.Equals(apiPrm.WebApiName,
                            StringComparison.CurrentCultureIgnoreCase));

                if (hasValue)
                {
                    var value = apiPrm.GetValue(context, prm);
                    ParameterDirection parameterDirection =
                            outputParameters.Any(p => p.SqlName.Equals(apiPrm.SqlName, StringComparison.CurrentCultureIgnoreCase)) ? ParameterDirection.InputOutput
                            : ParameterDirection.Input;
                    if (value is System.Data.DataTable dt)
                    {

                        var cmdPrm = cmd.CreateParameter();
                        cmdPrm.ParameterName = "@" + apiPrm.SqlName;
                        cmdPrm.Value = value;
                        cmdPrm.Direction = parameterDirection;
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
                            || spPrm.DbType.NetType == typeof(System.DateTimeOffset)
                            || spPrm.DbType.JsType == "number"
                            || spPrm.DbType.JsType == "integer"
                            || spPrm.DbType.JsType == "boolean")
                        {
                            cmd.AddCommandParameter(apiPrm.SqlName, DBNull.Value, configure: c =>
                            {
                                c.Direction = parameterDirection;
                            });
                        }
                        else
                        {
                            cmd.AddCommandParameter(apiPrm.SqlName, value, configure: c =>
                            {
                                c.Direction = parameterDirection;
                            });
                        }
                    }
                    else if (value == null)
                    {
                        sPParameters = sPParameters ?? sPParametersProvider.GetSPParameters(method.SP, con);
                        var spPrm = sPParameters.First(f => f.SqlName == apiPrm.SqlName);
                        cmd.AddCommandParameter(apiPrm.SqlName, DBNull.Value, spPrm.DbType.NetType, configure: c =>
                        {
                            c.Direction = parameterDirection;
                        });

                    }
                    else
                    {
                        cmd.AddCommandParameter(apiPrm.SqlName, value ?? System.DBNull.Value, configure: c =>
                        {
                            c.Direction = parameterDirection;
                        }); ;
                    }
                }
            }
            foreach (var op in outputParameters)
            {
                bool isAlreadyInput = inputParameters.Any(i => i.SqlName != null && i.SqlName.Equals(op.SqlName, StringComparison.CurrentCultureIgnoreCase));
                if (!isAlreadyInput)
                {
                    cmd.AddCommandParameter(op.SqlName, DBNull.Value, op.DbType.NetType, configure: c =>
                    {
                        c.Direction = ParameterDirection.Output;
                    });
                }
            }
            return cmd;
        }
    }
}

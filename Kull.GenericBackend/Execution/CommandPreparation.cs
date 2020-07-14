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

namespace Kull.GenericBackend.Execution
{
    public class CommandPreparation
    {
        private readonly ParameterProvider parameterProvider;
        private readonly SPParametersProvider sPParametersProvider;

        public CommandPreparation(
            ParameterProvider parameterProvider,
            SPParametersProvider sPParametersProvider)
        {
            this.parameterProvider = parameterProvider;
            this.sPParametersProvider = sPParametersProvider;
        }


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
            var (inputParameters, outputParameters) = parameterProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method, false));
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
                    else 
                    {
                        cmd.AddCommandParameter(apiPrm.SqlName, value);
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

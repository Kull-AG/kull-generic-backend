using Kull.GenericBackend.Common;
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
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Kull.Data;
using System.Linq;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Parameters;
using System.Data;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Execution;

public class CommandPreparation
{
    private readonly ParameterProvider parameterProvider;
    private readonly SPParametersProvider sPParametersProvider;

    public CommandPreparation(ParameterProvider parameterProvider, SPParametersProvider sPParametersProvider)
    {
        this.parameterProvider = parameterProvider;
        this.sPParametersProvider = sPParametersProvider;
    }

    /// <summary>
    /// Throws if the parameter name contains suspicous SQL
    /// </summary>
    /// <param name="sqlPrmName">The Sql Prm Name</param>
    /// <returns>The Sql Prm Name, otherwise throws</returns>
    protected string ValidateParamterName4Sql(string sqlPrmName)
    {
        char[] invalidChars = new char[] { '"', '\'', '\t', '\r', '\n', ' ', '-', '/', '*', '\\', '\0', '\b', (char)26 };
        foreach (char c in sqlPrmName)
        {
            if (invalidChars.Contains(c)) throw new InvalidOperationException("Cannot use that param name");
        }
        return sqlPrmName;
    }

    /// <summary>
    /// Creates the command object without (user) Parameters added
    /// </summary>
    /// <param name="con">The db connection</param>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="parameters"></param>
    /// <param name="parametersOfUser"></param>
    /// <returns></returns>
    protected virtual DbCommand CreateCommand(DbConnection con, DBObjectType type, DBObjectName name, IReadOnlyCollection<SPParameter>? parameters,
        IReadOnlyDictionary<string, object> parametersOfUser)
    {
        if (type == DBObjectType.StoredProcedure)
        {
            return con.CreateSPCommand(name);
        }
        else if (type == DBObjectType.TableOrView)
        {
            var cmd = con.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT " + GetSelectFromOData(parametersOfUser) + " FROM " + name.ToString(false, true)
                + GetWhereFromOData(parametersOfUser);
            return cmd;
        }
        else if (type == DBObjectType.TableValuedFunction)
        {
            var cmd = con.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT " + GetSelectFromOData(parametersOfUser) + " FROM " + name.ToString(false, true) + "(" +
               string.Join(", ", (parameters ?? Array.Empty<SPParameter>()).Where(p => p.ParameterDirection == ParameterDirection.Input).Select(p => "@" + ValidateParamterName4Sql(p.SqlName)))
               + ")" + GetWhereFromOData(parametersOfUser);
            return cmd;

        }
        else if (type == DBObjectType.ScalarFunction)
        {

            var cmd = con.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT " + name.ToString(false, true) + "(" +
                string.Join(", ", (parameters ?? Array.Empty<SPParameter>()).Where(p => p.ParameterDirection == ParameterDirection.Input).Select(p => "@" + ValidateParamterName4Sql(p.SqlName)))
                + ")";
            return cmd;
        }
        else
        {
            throw new InvalidOperationException("Not supported " + type.ToString());
        }
    }

    protected string GetWhereFromOData(IReadOnlyDictionary<string, object> parametersOfUser)
    {
        return string.Empty;
    }

    protected string GetSelectFromOData(IReadOnlyDictionary<string, object> parametersOfUser)
    {
        return "*";
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
    public virtual async Task<(DbCommand cmd, IReadOnlyCollection<OutputParameter> outputParameters)> GetCommandWithParameters(HttpContext? context,
            Func<string, object>? getRouteValue,
            DbConnection con,
            Entity ent,
            Method method,
            IReadOnlyDictionary<string, object> parameterOfUser)
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
        await con.AssureOpenAsync();
        IReadOnlyCollection<SPParameter>? sPParameters = method.DbObjectType == DBObjectType.StoredProcedure ? null : Array.Empty<SPParameter>();
        if (method.DbObjectType == DBObjectType.TableValuedFunction || method.DbObjectType == DBObjectType.ScalarFunction)
        {
            sPParameters = await sPParametersProvider.GetSPParameters(method.DbObject, con);//Always need that parameter
        }
        var cmd = CreateCommand(con, method.DbObjectType, method.DbObject, sPParameters, parameterOfUser);
        if (method.CommandTimeout != null)
        {
            cmd.CommandTimeout = method.CommandTimeout.Value;
        }
        var (inputParameters, outputParameters) = await parameterProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method, false), con);

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
                var value = apiPrm.GetValue(context, prm, new ApiParameterContext(ent, method));
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
                    sPParameters = sPParameters ?? await sPParametersProvider.GetSPParameters(method.DbObject, con);
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
                    sPParameters = sPParameters ?? await sPParametersProvider.GetSPParameters(method.DbObject, con);
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
            bool isAlreadyInput = cmd.Parameters.Cast<DbParameter>()
                .Any(p => (p.ParameterName.StartsWith("@") ? p.ParameterName.Substring(1) : p.ParameterName).Equals(op.SqlName, StringComparison.CurrentCultureIgnoreCase));
            if (!isAlreadyInput)
            {
                cmd.AddCommandParameter(op.SqlName, DBNull.Value, op.DbType.NetType, configure: c =>
                {
                    c.Direction = ParameterDirection.Output;
                    c.DbType = op.DbType.DataDbType;
                    if (op.MaxLength != null)
                    {
                        c.Size = op.MaxLength.Value == -1 ? -1 : (op.MaxLength.Value * op.DbType.BytesPerChar);
                    }
                    else if (op.DbType.DbType == "timestamp")
                    {
                        c.Size = 8;
                    }
                    else if (op.DbType.NetType == typeof(Byte[]))
                    {
                        c.Size = -1;
                    }
                });
            }
        }
        return (cmd, outputParameters ?? Array.Empty<OutputParameter>());
    }
}

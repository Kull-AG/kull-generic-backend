﻿using System.Data.Common;
using System.Runtime.CompilerServices;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.Parameters;
using Microsoft.AspNetCore.Http;
using Kull.Data;
using DynamicODataToSQL;
using SqlKata.Compilers;
using System.Data;

namespace Kull.GenericBackend.OData;
public class CommandPreparationOData : Kull.GenericBackend.Execution.CommandPreparation
{
    internal static readonly string[] ODataPatermers = new string[] { "select", "filter", "orderby", "top", "skip", "apply", "expand", "lambda" };

    public CommandPreparationOData(ParameterProvider parameterProvider, SPParametersProvider sPParametersProvider) : base(parameterProvider, sPParametersProvider)
    {
    }

    protected override DbCommand CreateCommand(DbConnection con, DBObjectType type, DBObjectName name, IReadOnlyCollection<SPParameter>? parameters,
        IReadOnlyDictionary<string, object> parametersOfUser)
    {
        var converter = new ODataToSqlConverter(new EdmModelBuilder(), new SqlServerCompiler() { UseLegacyPagination = false });
        if (type == DBObjectType.TableOrView || type == DBObjectType.TableValuedFunction)
        {
            var placeHolderTableName = "____placeholder_____";
            var tableName = type == DBObjectType.TableValuedFunction ? placeHolderTableName : name.ToString(false, true);

            var odataQueryParams = parametersOfUser.Where(p => p.Key.StartsWith("$") &&
                 ODataPatermers.Contains(p.Key.Substring(1)))
                 .ToDictionary(k => k.Key.Substring(1), k => (string)k.Value);
            if (odataQueryParams.Count == 0) return base.CreateCommand(con, type, name, parameters, parametersOfUser);
            var (sql, sqlPrmValues) = converter.ConvertToSQL(
                           tableName,
                           odataQueryParams,
                           false);
            var cmd = con.CreateCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = sql;
            if (type == DBObjectType.TableValuedFunction)
            {
                sql = sql.Replace(placeHolderTableName, "SELECT * FROM " + name.ToString(false, true) + "(" +
               string.Join(", ", (parameters ?? Array.Empty<SPParameter>()).Where(p => p.ParameterDirection == ParameterDirection.Input).Select(p => "@" + ValidateParamterName4Sql(p.SqlName)))
               + ")");
            }
            foreach (var p in sqlPrmValues)
            {
                cmd.AddCommandParameter(p.Key, p.Value);
            }
            return cmd;
        }
        return base.CreateCommand(con, type, name, parameters, parametersOfUser);
    }

}

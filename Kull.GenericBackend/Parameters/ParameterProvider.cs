using Kull.Data;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.SwaggerGeneration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Parameters;

public sealed class ParameterProvider
{
    public IEnumerable<Filter.IParameterInterceptor> parameterInterceptors;
    private readonly SPParametersProvider sPParametersProvider;
    private readonly Common.NamingMappingHandler namingMappingHandler;
    private readonly CodeConvention convention;
    private readonly SqlHelper sqlHelper;

    public ParameterProvider(IEnumerable<Filter.IParameterInterceptor> parameterInterceptors,
        SPParametersProvider sPParametersProvider,
        Common.NamingMappingHandler namingMappingHandler,
        SwaggerGeneration.CodeConvention convention,
        SqlHelper sqlHelper)
    {
        this.parameterInterceptors = parameterInterceptors;
        this.sPParametersProvider = sPParametersProvider;
        this.namingMappingHandler = namingMappingHandler;
        this.convention = convention;
        this.sqlHelper = sqlHelper;
    }


    public async Task<(WebApiParameter[] inputParameters, OutputParameter[] outputParameters)> GetApiParameters(Filter.ParameterInterceptorContext context,
        IReadOnlyCollection<string> ignoreParameters,
        DbConnection dbConnection)
    {
        var method = context.Method;
        var spParamsRaw = await sPParametersProvider.GetSPParameters(method.DbObject, dbConnection);
        var spParams = ignoreParameters.Count == 0 ? spParamsRaw : spParamsRaw.Where(p => !ignoreParameters.Contains(p.SqlName.StartsWith("@") ? p.SqlName.Substring(1) : p.SqlName,
              StringComparer.OrdinalIgnoreCase));
        var spPrmsNoCount = spParams.Where(p => p.ParameterDirection != System.Data.ParameterDirection.Output);
        var prmsOutRaw = spParams.Where(p => p.ParameterDirection == System.Data.ParameterDirection.Output || p.ParameterDirection == System.Data.ParameterDirection.InputOutput);
        var prmsOut = prmsOutRaw.Select(p => new OutputParameter(p.SqlName, p.DbType)).ToArray();
        var webApiNames = namingMappingHandler.GetNames(spParams.Select(s => s.SqlName)).ToArray();
        var userDefinedTypes = spParams.Where(u => u.UserDefinedType != null).Select(u => u.UserDefinedType!).Distinct();
        Dictionary<DBObjectName, IReadOnlyCollection<SqlFieldDescription>> udtFields = new Dictionary<DBObjectName, IReadOnlyCollection<SqlFieldDescription>>();
        foreach (var t in userDefinedTypes)
        {
            udtFields.Add(t, await sqlHelper.GetTableTypeFields(dbConnection, t));
        }
        var apiParamsRaw = spParams.Select((s, index) =>
            (WebApiParameter)new DbApiParameter(s.SqlName, webApiNames[index],
               s.DbType, s.IsNullable, s.UserDefinedType, s.UserDefinedType == null ? null : udtFields[s.UserDefinedType],
                convention,
               namingMappingHandler)
        );
        var apiParams = new LinkedList<WebApiParameter>(apiParamsRaw);
        foreach (var inter in parameterInterceptors)
        {
            inter.Intercept(apiParams, context);
        }
        return (apiParams.ToArray(), prmsOut);
    }
}

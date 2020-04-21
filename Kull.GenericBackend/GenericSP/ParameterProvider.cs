using Kull.Data;
using Kull.DatabaseMetadata;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.GenericSP
{
    public class ParameterProvider
    {
        public IEnumerable<Filter.IParameterInterceptor> parameterInterceptors;
        private readonly DbConnection dbConnection;
        private readonly SPParametersProvider sPParametersProvider;
        private readonly Model.NamingMappingHandler namingMappingHandler;
        private readonly SqlHelper sqlHelper;

        public ParameterProvider(IEnumerable<Filter.IParameterInterceptor> parameterInterceptors,
            DbConnection dbConnection,
            SPParametersProvider sPParametersProvider,
            Model.NamingMappingHandler namingMappingHandler,
            SqlHelper sqlHelper)
        {
            this.parameterInterceptors = parameterInterceptors;
            this.dbConnection = dbConnection;
            this.sPParametersProvider = sPParametersProvider;
            this.namingMappingHandler = namingMappingHandler;
            this.sqlHelper = sqlHelper;
        }

        public Parameters.WebApiParameter[] GetApiParameters(Filter.ParameterInterceptorContext context)
        {
            var method = context.Method;
            var spParams = sPParametersProvider.GetSPParameters(method.SP, dbConnection);
            var webApiNames = namingMappingHandler.GetNames(spParams.Select(s => s.SqlName)).ToArray();

            var apiParamsRaw = spParams.Select((s, index) =>
                (Parameters.WebApiParameter) new Parameters.DbApiParameter(s.SqlName, webApiNames[index],
                   s.DbType, s.IsNullable, s.UserDefinedType, sqlHelper, namingMappingHandler)
            );
            var apiParams = new LinkedList<Parameters.WebApiParameter>(apiParamsRaw);
            foreach(var inter in parameterInterceptors)
            {
                inter.Intercept(apiParams, context);
            }
            return apiParams.ToArray();
        }
    }
}

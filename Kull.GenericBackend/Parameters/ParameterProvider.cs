using Kull.DatabaseMetadata;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace Kull.GenericBackend.Parameters
{
    public class ParameterProvider
    {
        public IEnumerable<Filter.IParameterInterceptor> parameterInterceptors;
        private readonly SPParametersProvider sPParametersProvider;
        private readonly Common.NamingMappingHandler namingMappingHandler;
        private readonly SqlHelper sqlHelper;

        public ParameterProvider(IEnumerable<Filter.IParameterInterceptor> parameterInterceptors,
            SPParametersProvider sPParametersProvider,
            Common.NamingMappingHandler namingMappingHandler,
            SqlHelper sqlHelper)
        {
            this.parameterInterceptors = parameterInterceptors;
            this.sPParametersProvider = sPParametersProvider;
            this.namingMappingHandler = namingMappingHandler;
            this.sqlHelper = sqlHelper;
        }

        public WebApiParameter[] GetApiParameters(Filter.ParameterInterceptorContext context, DbConnection dbConnection)
        {
            var method = context.Method;
            var spParams = sPParametersProvider.GetSPParameters(method.SP, dbConnection);
            var webApiNames = namingMappingHandler.GetNames(spParams.Select(s => s.SqlName)).ToArray();

            var apiParamsRaw = spParams.Select((s, index) =>
                (WebApiParameter)new DbApiParameter(s.SqlName, webApiNames[index],
                   s.DbType, s.IsNullable, s.UserDefinedType, sqlHelper, namingMappingHandler)
            );
            var apiParams = new LinkedList<WebApiParameter>(apiParamsRaw);
            foreach (var inter in parameterInterceptors)
            {
                inter.Intercept(apiParams, context);
            }
            return apiParams.ToArray();
        }
    }
}

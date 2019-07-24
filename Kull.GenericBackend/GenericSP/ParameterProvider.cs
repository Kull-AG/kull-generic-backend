using Kull.Data;
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
        private readonly Model.SPParametersProvider sPParametersProvider;
        private readonly Model.NamingMappingHandler namingMappingHandler;
        private readonly Model.SqlHelper sqlHelper;

        public ParameterProvider(IEnumerable<Filter.IParameterInterceptor> parameterInterceptors,
            DbConnection dbConnection,
            Model.SPParametersProvider sPParametersProvider,
            Model.NamingMappingHandler namingMappingHandler,
            Model.SqlHelper sqlHelper)
        {
            this.parameterInterceptors = parameterInterceptors;
            this.dbConnection = dbConnection;
            this.sPParametersProvider = sPParametersProvider;
            this.namingMappingHandler = namingMappingHandler;
            this.sqlHelper = sqlHelper;
        }

        public SwaggerGeneration.WebApiParameter[] GetApiParameters(Entity ent, DBObjectName sp)
        {
            var spParams = sPParametersProvider.GetSPParameters(sp, dbConnection);
            var webApiNames = namingMappingHandler.GetNames(spParams.Select(s => s.SqlName)).ToArray();

            var apiParamsRaw = spParams.Select((s, index) =>
                (SwaggerGeneration.WebApiParameter) new SwaggerGeneration.DbApiParameter(s.SqlName, webApiNames[index],
                   s.DbType, s.IsNullable, s.UserDefinedType, sqlHelper, namingMappingHandler)
            );
            var apiParams = new LinkedList<SwaggerGeneration.WebApiParameter>(apiParamsRaw);
            foreach(var inter in parameterInterceptors)
            {
                inter.Intercept(apiParams);
            }
            return apiParams.ToArray();
        }
    }
}

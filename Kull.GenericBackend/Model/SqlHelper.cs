using Kull.Data;
using Kull.GenericBackend.GenericSP;
using Kull.GenericBackend.Model;
using Kull.GenericBackend.SwaggerGeneration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.Model
{
    /// <summary>
    /// A utility class for getting Types of User Defined Types and for getting the expected result from  query
    /// </summary>
    public class SqlHelper
    {
        private readonly NamingMappingHandler namingMappingHandler;
        private readonly IHostingEnvironment hostingEnvironment;
        private readonly ILogger<SqlHelper> logger;
        private readonly SwaggerFromSPOptions swaggerFromSPOptions;

        public string GetParameterObjectName(Entity ent, string HttpMethod, Method method) =>
                 ent.GetDisplayString() + ToCamelCase(HttpMethod) + "Parameters";

        private string ToCamelCase(string key)=> key[0].ToString().ToUpper() + key.Substring(1).ToLower();

        public SqlHelper(NamingMappingHandler namingMappingHandler, IHostingEnvironment hostingEnvironment,
                ILogger<SqlHelper> logger,
                SwaggerFromSPOptions swaggerFromSPOptions)
        {
            this.namingMappingHandler = namingMappingHandler;
            this.hostingEnvironment = hostingEnvironment;
            this.logger = logger;
            this.swaggerFromSPOptions = swaggerFromSPOptions;
        }

        public ISqlMappedData[] GetTableTypeFields(DbConnection dbConnection,
         DBObjectName tableType)
        {
            string sql = $@"
SELECT c.name as ColumnName,
	CASE WHEN t.name ='sysname' THEN 'nvarchar' ELSE t.name END AS TypeName,
	c.is_nullable
FROM sys.columns c
	inner join sys.types t ON t.user_type_id=c.user_type_id
WHERE object_id IN (
  SELECT tt.type_table_object_id
  FROM sys.table_types tt 
	inner join sys.schemas sc ON sc.schema_id=tt.schema_id
  WHERE tt.name = @Name and sc.name=@Schema
);";
            var cmd = dbConnection.AssureOpen().CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.AddCommandParameter("@Name", tableType.Name);
            cmd.AddCommandParameter("@Schema", tableType.Schema);
            List<SqlFieldDescription> list = new List<SqlFieldDescription>();
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    list.Add(new SqlFieldDescription()
                    {
                        IsNullable = rdr.GetBoolean("is_nullable"),
                        Name = rdr.GetNString("ColumnName"),
                        TypeName = SqlType.GetSqlType(rdr.GetNString("TypeName"))
                    });
                }
            }
            namingMappingHandler.SetNames(list);
            return list.ToArray();
        }

        public ISqlMappedData[] GetSPResultSet(
            DbConnection con, DBObjectName model)
        {
            ISqlMappedData[] dataToWrite = null;
            var sp_desc_paths = swaggerFromSPOptions.PersistResultSets ? System.IO.Path.Combine(hostingEnvironment.ContentRootPath,
                                "ResultSets") : null;
            var cachejsonFile = swaggerFromSPOptions.PersistResultSets ? System.IO.Path.Combine(sp_desc_paths,
                model.ToString() + ".json") : null;
            try
            {
                List<Model.SqlFieldDescription> resultSet = new List<SqlFieldDescription>();
                using (var rdr = con.AssureOpen().CreateSP("sp_describe_first_result_set")
                    .AddCommandParameter("tsql", model.ToString())
                    .ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        resultSet.Add(new SqlFieldDescription()
                        {
                            Name = rdr.GetNString("name"),
                            TypeName = SqlType.GetSqlType(rdr.GetNString("system_type_name")),
                            IsNullable = rdr.GetBoolean("is_nullable")
                        });
                    }
                }
                if (swaggerFromSPOptions.PersistResultSets)
                {
                    if (resultSet.Count > 0)
                    {
                        try
                        {

                            if (!System.IO.Directory.Exists(sp_desc_paths))
                            {
                                System.IO.Directory.CreateDirectory(sp_desc_paths);
                            }
                            var jsAr = new JArray(resultSet.Select(s => s.Serialize()).ToArray());
                            var json = JsonConvert.SerializeObject(jsAr, Formatting.Indented);

                            System.IO.File.WriteAllText(cachejsonFile, json);

                        }
                        catch (Exception ercache)
                        {
                            logger.LogWarning("Could not cache Results set of {0}. Reason:\r\n{1}", model, ercache);
                        }
                    }
                }
                dataToWrite = resultSet
                    .Cast<Model.ISqlMappedData>()
                    .ToArray();

            }
            catch (Exception err)
            {
                logger.LogError(err, $"Error getting result set from {model}");
                dataToWrite = new ISqlMappedData[] { };

            }

            if (dataToWrite.Length == 0 && swaggerFromSPOptions.PersistResultSets && System.IO.File.Exists(cachejsonFile))
            {
                try
                {
                    // Not Sucessfully gotten data
                    var json = System.IO.File.ReadAllText(cachejsonFile);
                    var resJS = JsonConvert.DeserializeObject<JArray>(json);
                    var res = resJS.Select(s => SqlFieldDescription.FromJObject((JObject)s)).ToArray();
                    namingMappingHandler.SetNames(res);
                    return res.Cast<ISqlMappedData>().ToArray();
                }
                catch (Exception err)
                {
                    logger.LogWarning("Could not get cache {0}. Reason:\r\n{1}", model, err);
                }
            }
            namingMappingHandler.SetNames(dataToWrite);
            return dataToWrite;
        }
    }
}

using Kull.GenericBackend.GenericSP;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Data.Common;
using Kull.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Kull.GenericBackend.Model;

namespace Kull.GenericBackend.SwaggerGeneration
{
    /// <summary>
    /// The filter for swashbuckle that applies the Infos from the SP's
    /// </summary>
    public class DatabaseOperations : IDocumentFilter
    {
        private readonly IReadOnlyCollection<Entity> entities;
        private readonly SPMiddlewareOptions sPMiddlewareOptions;
        private readonly SwaggerFromSPOptions options;
        private readonly SqlHelper sqlHelper;
        private readonly ILogger logger;
        private readonly DbConnection dbConnection;
        private readonly Model.SPParametersProvider sPParametersProvider;

        public DatabaseOperations(Microsoft.Extensions.Configuration.IConfiguration conf,
         SPMiddlewareOptions sPMiddlewareOptions,
         SwaggerFromSPOptions options,
         SqlHelper sqlHelper,
         ILogger<DatabaseOperations> logger,
         DbConnection dbConnection,
         Model.SPParametersProvider sPParametersProvider)
        {
            this.sPMiddlewareOptions = sPMiddlewareOptions;
            this.options = options;
            this.sqlHelper = sqlHelper;
            this.logger = logger;
            this.dbConnection = dbConnection;
            this.sPParametersProvider = sPParametersProvider;
            var ent = conf.GetSection("Entities");
            entities = ent.GetChildren()
                   .Select(s => Entity.GetFromSection(s)).ToList();
        }


        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {

            foreach (var ent in entities)
            {
                if (ent.Methods.Any())
                {
                    OpenApiPathItem openApiPathItem = new OpenApiPathItem();
                    if (openApiPathItem.Operations == null)
                        openApiPathItem.Operations = new Dictionary<OperationType, OpenApiOperation>();

                    foreach (var method in ent.Methods)
                    {
                        var opType = (OperationType)Enum.Parse(typeof(OperationType), method.Key, true);
                        OpenApiOperation bodyOperation = new OpenApiOperation();
                        WriteBodyPath(dbConnection, bodyOperation, ent, opType, method.Value);
                        openApiPathItem.Operations.Add(opType, bodyOperation);
                    }
                    swaggerDoc.Paths.Add(ent.GetUrl(this.sPMiddlewareOptions.Prefix, false), openApiPathItem);
                }
            }


            var allModels = entities.SelectMany(e => e.Methods)
                    .Select(s => s.Value.SP)
                   .Distinct();
            foreach (var model in allModels)
            {
                OpenApiSchema resultSchema = new OpenApiSchema();
                ISqlMappedData[] dataToWrite = sqlHelper.GetSPResultSet(dbConnection, model);
                WriteJsonSchema(resultSchema, dataToWrite);
                swaggerDoc.Components.Schemas.Add(model.Name + "Result", resultSchema);

            }

            foreach (var ent in entities)
            {
                foreach (var method in ent.Methods)
                {
                    ISqlMappedData[] props = GetBodyOrQueryStringParameters(dbConnection, ent, method.Value.SP);

                    foreach (var up in props.Where(p => p.UserDefinedType != null))
                    {
                        string name = GetSqlTypeWebApiName(up.UserDefinedType);
                        if (!swaggerDoc.Components.Schemas.ContainsKey(name))
                        {
                            OpenApiSchema tableTypeSchema = new OpenApiSchema();
                            var tableCols = sqlHelper.GetTableTypeFields(dbConnection, up.UserDefinedType);
                            WriteJsonSchema(tableTypeSchema, tableCols);
                            swaggerDoc.Components.Schemas.Add(name,
                                tableTypeSchema);
                        }
                    }
                    if (props.Any())
                    {
                        OpenApiSchema parameterSchema = new OpenApiSchema();
                        WriteJsonSchema(parameterSchema, props);
                        swaggerDoc.Components.Schemas.Add(sqlHelper.GetParameterObjectName(ent, method.Key, method.Value),
                            parameterSchema);
                    }
                }
            }

        }





        private static string GetSqlTypeWebApiName(DBObjectName userDefinedType)
        {
            return (userDefinedType.Schema == "dbo" ? "" :
                                            userDefinedType.Schema + ".") + userDefinedType.Name;
        }

        private ISqlMappedData[] GetBodyOrQueryStringParameters(DbConnection con, Entity ent, DBObjectName sp)
        {
            return sPParametersProvider.GetSPParameters(sp, con)
                .Where(s => s.WebApiName != null && !ent.ContainsPathParameter(s.WebApiName))
                .Cast<ISqlMappedData>()
                .ToArray();
        }

        private static void WriteJsonSchema(OpenApiSchema schema, IEnumerable<ISqlMappedData> props)
        {
            schema.Type = "object";
            foreach (var prop in props)
            {
                if (prop.WebApiName != null)
                {// A system field like 'ADLogin' has WebApiName = null

                    OpenApiSchema property = new OpenApiSchema();
                    property.Type = prop.DbType.JsType;
                    if (prop.DbType.JsFormat != null)
                    {
                        property.Format = prop.DbType.JsFormat;
                    }
                    property.Nullable = prop.IsNullable;

                    if (prop.UserDefinedType != null)
                    {
                        property.UniqueItems = false;
                        property.Items = new OpenApiSchema()
                        {
                            Reference = new OpenApiReference()
                            {
                                Type = ReferenceType.Schema,
                                Id = GetSqlTypeWebApiName(prop.UserDefinedType)
                            }
                        };
                    }

                    schema.Properties.Add(prop.WebApiName, property);
                }
            }
        }


        private void WriteBodyPath(DbConnection con, OpenApiOperation operation, Entity ent, OperationType operationType, Method method)
        {
            if (operation.Tags == null)
                operation.Tags = new List<OpenApiTag>();
            operation.Tags.Add(new OpenApiTag() { Name = ent.GetDisplayString() });
            operation.OperationId = (
                operationType == OperationType.Post &&
                    (method.SP.Name.StartsWith("spAddUpdate") ||
                     method.SP.Name.StartsWith("sp_AddUpdate") ||
                     method.SP.Name.EndsWith("_AddUpdate")
                    ) ? "AddUpdate" :
                operationType == OperationType.Post ? "Add" :
                operationType == OperationType.Put ? "Update" :
                operationType == OperationType.Delete ? "Delete" :
                operationType == OperationType.Get ? "Get" :
                method.HttpMethod)
                + ent.GetDisplayString();
            OpenApiResponse response = new OpenApiResponse();
            response.Content.Add("application/json", new OpenApiMediaType()
            {
                Schema =
                    new OpenApiSchema()
                    {
                        Type = "array",
                        UniqueItems = false,
                        Items = new OpenApiSchema()
                        {
                            Reference = new OpenApiReference()
                            {
                                Type = ReferenceType.Schema,
                                Id = method.SP.Name + "Result"
                            }
                        }
                    }

            });
            operation.Responses.Add("200", response);



            var props = sPParametersProvider.GetSPParameters(method.SP, con)
                                .ToArray();
            if (operationType != OperationType.Get && props.Any(p => p.WebApiName != null && !ent.ContainsPathParameter(p.WebApiName)))
            {
                if (operation.RequestBody == null) operation.RequestBody = new OpenApiRequestBody();
                operation.RequestBody.Required = true;
                operation.RequestBody.Description = "Parameters for " + method.SP.ToString();
                operation.RequestBody.Content.Add("application/json", new OpenApiMediaType()
                {
                    Schema = new OpenApiSchema()
                    {
                        Reference = new OpenApiReference()
                        {
                            Type = ReferenceType.Schema,
                            Id = sqlHelper.GetParameterObjectName(ent, method.HttpMethod, method)
                        }
                    }
                });
            }
            if (operation.Parameters == null) operation.Parameters = new List<OpenApiParameter>();
            if (operationType == OperationType.Get)
            {
                foreach (var item in props.Where(p => p.WebApiName != null && !ent.ContainsPathParameter(p.WebApiName)))
                {
                    (string type, string format) = item.GetJSType();

                    operation.Parameters.Add(new OpenApiParameter()
                    {
                        Name = item.WebApiName,
                        In = ParameterLocation.Query,
                        Required = false,
                        Schema = new OpenApiSchema()
                        {
                            Type = type,
                            Format = format
                        }
                    });
                }

            }
            foreach (var item in props.Where(p => p.WebApiName != null && ent.ContainsPathParameter(p.WebApiName)))
            {
                (string type, string format) = item.GetJSType();
                operation.Parameters.Add(new OpenApiParameter()
                {
                    Name = item.WebApiName,
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema()
                    {
                        Type = type,
                        Format = format
                    }
                });
            }



        }
    }
}
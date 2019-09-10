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
using Kull.DatabaseMetadata;

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
        private readonly ParameterProvider parametersProvider;
        private readonly NamingMappingHandler namingMappingHandler;

        public DatabaseOperations(Microsoft.Extensions.Configuration.IConfiguration conf,
         SPMiddlewareOptions sPMiddlewareOptions,
         SwaggerFromSPOptions options,
         SqlHelper sqlHelper,
         ILogger<DatabaseOperations> logger,
         DbConnection dbConnection,
         ParameterProvider parametersProvider,
         NamingMappingHandler namingMappingHandler)
        {
            this.sPMiddlewareOptions = sPMiddlewareOptions;
            this.options = options;
            this.sqlHelper = sqlHelper;
            this.logger = logger;
            this.dbConnection = dbConnection;
            this.parametersProvider = parametersProvider;
            this.namingMappingHandler = namingMappingHandler;
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
                var dataToWrite = sqlHelper.GetSPResultSet(model, options.PersistResultSets);
                WriteJsonSchema(resultSchema, dataToWrite, namingMappingHandler);
                swaggerDoc.Components.Schemas.Add(model.Name + "Result", resultSchema);

            }

            foreach (var ent in entities)
            {
                foreach (var method in ent.Methods)
                {
                    var parameters = GetBodyOrQueryStringParameters(ent, method.Value.SP);
                    var addTypes = parameters.SelectMany(sm => sm.GetRequiredTypes()).Distinct();
                    foreach (var addType in addTypes)
                    {
                        if (!swaggerDoc.Components.Schemas.ContainsKey(addType.WebApiName))
                        {
                            OpenApiSchema tableTypeSchema = addType.GetSchema();

                            swaggerDoc.Components.Schemas.Add(addType.WebApiName,
                                tableTypeSchema);
                        }

                    }
                    if (parameters.Any())
                    {
                        OpenApiSchema parameterSchema = new OpenApiSchema();
                        WriteJsonSchema(parameterSchema, parameters);


                        swaggerDoc.Components.Schemas.Add(Method.GetParameterObjectName(ent, method.Key, method.Value),
                            parameterSchema);
                    }
                }
            }

        }


        private WebApiParameter[] GetBodyOrQueryStringParameters(Entity ent, DBObjectName sp)
        {
            return parametersProvider.GetApiParameters(ent, sp)
                .Where(s => s.WebApiName != null && !ent.ContainsPathParameter(s.WebApiName))
                .ToArray();
        }

        private void WriteJsonSchema(OpenApiSchema parameterSchema, WebApiParameter[] parameters)
        {
            parameterSchema.Type = "object";
            foreach (var item in parameters)
            {
                parameterSchema.Properties.Add(
                     item.WebApiName,
                    item.GetSchema());
            }
        }

        private static void WriteJsonSchema(OpenApiSchema schema,
            IEnumerable<SqlFieldDescription> props,
            NamingMappingHandler namingMappingHandler)
        {
            schema.Type = "object";
            var names = namingMappingHandler.GetNames(props.Select(p => p.Name))
                .GetEnumerator();
            foreach (var prop in props)
            {

                OpenApiSchema property = new OpenApiSchema();
                property.Type = prop.DbType.JsType;
                if (prop.DbType.JsFormat != null)
                {
                    property.Format = prop.DbType.JsFormat;
                }
                property.Nullable = prop.IsNullable;

                names.MoveNext();
                schema.Properties.Add(names.Current, property);
            }
        }



        private void WriteBodyPath(DbConnection con, OpenApiOperation operation, Entity ent, OperationType operationType, Method method)
        {
            if (operation.Tags == null)
                operation.Tags = new List<OpenApiTag>();
            operation.Tags.Add(new OpenApiTag() { Name = ent.GetDisplayString() });

            var operationId = method.OperationId;
            if(method.OperationId == null)
            {
                operationId = (
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
            }
            operation.OperationId = operationId;
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



            var props = parametersProvider.GetApiParameters(ent, method.SP)
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
                            Id = Method.GetParameterObjectName(ent, method.HttpMethod, method)
                        }
                    }
                });
            }
            if (operation.Parameters == null) operation.Parameters = new List<OpenApiParameter>();
            if (operationType == OperationType.Get)
            {
                foreach (var item in props.Where(p => p.WebApiName != null && !ent.ContainsPathParameter(p.WebApiName)))
                {
                    var schema = item.GetSchema();
                    
                    operation.Parameters.Add(new OpenApiParameter()
                    {
                        Name = item.WebApiName,
                        In = ParameterLocation.Query,
                        Required = false,
                        Schema = new OpenApiSchema()
                        { // IsNullable etc are ignore intentionally
                            Type = schema.Type,
                            Format = schema.Format
                        }
                    });
                }

            }
            foreach (var item in props.Where(p => p.WebApiName != null && ent.ContainsPathParameter(p.WebApiName)))
            {
                var schema = item.GetSchema();
                operation.Parameters.Add(new OpenApiParameter()
                {
                    Name = item.WebApiName,
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema()
                    {
                        Type = schema.Type,
                        Format = schema.Format
                    }
                });
            }



        }
    }
}
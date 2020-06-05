using Kull.GenericBackend.GenericSP;
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Data.Common;
using Kull.GenericBackend.Common;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Serialization;
using Kull.GenericBackend.Parameters;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Any;
using Kull.GenericBackend.Config;
using System;
#if NETFX
using Swashbuckle.Swagger;
using Kull.MvcCompat;
using System.Web.Http.Description;
#else
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.Extensions.Logging;
#endif

namespace Kull.GenericBackend.SwaggerGeneration
{
#if NET47
    public class DatabaseOperationWrap : IDocumentFilter
    {
        public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer)
        {
            IDocumentFilter realFilter = (IDocumentFilter) System.Web.Mvc.DependencyResolver.Current.GetService(typeof(IDocumentFilter));
            realFilter.Apply(swaggerDoc, schemaRegistry, apiExplorer);
        }
    }
#endif

    /// <summary>
    /// The filter for swashbuckle that applies the Infos from the SP's
    /// </summary>
    public class DatabaseOperations : IDocumentFilter
    {
        private readonly IReadOnlyCollection<Entity> entities;
        private readonly SPMiddlewareOptions sPMiddlewareOptions;
        private readonly SwaggerFromSPOptions options;
        private readonly SqlHelper sqlHelper;
        private readonly ILogger<DatabaseOperations> logger;
        private readonly SerializerResolver serializerResolver;
        private readonly DbConnection dbConnection;
        private readonly ParameterProvider parametersProvider;
        private readonly NamingMappingHandler namingMappingHandler;
        private readonly CodeConvention codeConvention;

        public DatabaseOperations(
         SPMiddlewareOptions sPMiddlewareOptions,
         SwaggerFromSPOptions options,
         SqlHelper sqlHelper,
         ILogger<DatabaseOperations> logger,
         DbConnection dbConnection,
         ParameterProvider parametersProvider,
         SerializerResolver serializerResolver,
         NamingMappingHandler namingMappingHandler,
         CodeConvention codeConvention,
         ConfigProvider configProvider)
        {
            this.codeConvention = codeConvention;
            this.sPMiddlewareOptions = sPMiddlewareOptions;
            this.options = options;
            this.sqlHelper = sqlHelper;
            this.logger = logger;
            this.serializerResolver = serializerResolver;
            this.dbConnection = dbConnection;
            this.parametersProvider = parametersProvider;
            this.namingMappingHandler = namingMappingHandler;
            entities = configProvider.Entities;
        }

#if NET47
        public class DocumentFilterContext { }
#endif

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Paths == null) swaggerDoc.Paths = new OpenApiPaths();
            if (swaggerDoc.Components == null) swaggerDoc.Components = new OpenApiComponents();
            if (swaggerDoc.Components.Schemas == null) swaggerDoc.Components.Schemas = new Dictionary<string, OpenApiSchema>();

            foreach (var ent in entities)
            {
                if (ent.Methods.Any())
                {
                    OpenApiPathItem openApiPathItem = new OpenApiPathItem();
                    if (openApiPathItem.Operations == null)
                        openApiPathItem.Operations = new Dictionary<OperationType, OpenApiOperation>();

                    foreach (var method in ent.Methods)
                    {
                        var opType = method.Key;
                        OpenApiOperation bodyOperation = new OpenApiOperation();
                        WriteBodyPath(bodyOperation, ent, opType, method.Value);
                        openApiPathItem.Operations.Add(opType, bodyOperation);
                    }
                    swaggerDoc.Paths.Add(ent.GetUrl(this.sPMiddlewareOptions.Prefix, false), openApiPathItem);
                }
            }


            var allMethods = entities.SelectMany(e => e.Methods.Values);
            foreach (var method in allMethods)
            {
                string typeName = codeConvention.GetResultTypeName(method);
                if (swaggerDoc.Components.Schemas.ContainsKey(typeName))
                {
                    logger.LogWarning($"Type {typeName} already exists in Components. Assuming it's the same");
                }
                else
                {
                    OpenApiSchema resultSchema = new OpenApiSchema();
                    var dataToWrite = sqlHelper.GetSPResultSet(method.SP, options.PersistResultSets);
                    WriteJsonSchema(resultSchema, dataToWrite, namingMappingHandler);
                    swaggerDoc.Components.Schemas.Add(typeName, resultSchema);
                }
            }

            foreach (var ent in entities)
            {
                foreach (var method in ent.Methods)
                {
                    var parameters = GetBodyOrQueryStringParameters(ent, method.Value);
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


                        swaggerDoc.Components.Schemas.Add(codeConvention.GetParameterObjectName(ent, method.Value),
                            parameterSchema);
                    }
                }
            }

        }


        private Parameters.WebApiParameter[] GetBodyOrQueryStringParameters(Entity ent, Method method)
        {
            return parametersProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method, true))
                .inputParameters
                .Where(s => s.WebApiName != null && !ent.ContainsPathParameter(s.WebApiName))
                .ToArray();
        }

        private void WriteJsonSchema(OpenApiSchema parameterSchema, Parameters.WebApiParameter[] parameters)
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
            if (schema.Xml == null) schema.Xml = new OpenApiXml();
            schema.Xml.Name = "tr";//it's always tr
            if (schema.Required == null)
                schema.Required = new HashSet<string>();
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
                schema.Required.Add(names.Current);
            }
        }


        protected virtual OpenApiResponses GetDefaultResponse(string resultTypeName, string? outputObjectName,
                OperationResponseContext context)
        {
            OpenApiResponses responses = new OpenApiResponses();
            OpenApiResponse response = new OpenApiResponse();
            response.Description = $"OK"; // Required as per spec

            OpenApiSchema arrayOfResult =
                    new OpenApiSchema()
                    {
                        Type = "array",
                        Xml = new OpenApiXml()
                        {
                            Name = "table"
                        },
                        UniqueItems = false,
                        Items = new OpenApiSchema()
                        {
                            Reference = new OpenApiReference()
                            {
                                Type = ReferenceType.Schema,
                                Id = resultTypeName
                            }
                        }
                    };
            OpenApiSchema schema = arrayOfResult;
            if (outputObjectName != null || context.AlwaysWrapJson)
            {
                schema = new OpenApiSchema()
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>()
                   {
                       {"result", arrayOfResult }
                   }
                };
                if (outputObjectName != null)
                {
                    schema.Properties.Add("output", new OpenApiSchema()
                    {
                        Reference = new OpenApiReference()
                        {
                            Type = ReferenceType.Schema,
                            Id = outputObjectName
                        }
                    });
                }
            }
            response.Content.Add("application/json", new OpenApiMediaType()
            {
                Schema = schema

            });
            responses.Add("200", response);
            return responses;
        }


        private void WriteBodyPath(OpenApiOperation operation, Entity entity, OperationType operationType, Method method)
        {
            if (operation.Tags == null)
                operation.Tags = new List<OpenApiTag>();
            operation.Tags.Add(new OpenApiTag()
            {
                Name =
                method.Tag != null ? method.Tag :
                entity.Tag != null ? entity.Tag :
                codeConvention.GetTag(entity, method)
            });

            var operationId = method.OperationId;
            if (method.OperationId == null)
            {
                operationId = codeConvention.GetOperationId(entity, method);
            }
            operation.OperationId = operationId;
            if (method.OperationName != null || method.OperationId == null)
            {
                operation.AddExtension("x-operation-name", new OpenApiString(method.OperationName ?? codeConvention.GetOperationName(entity, method)));
            }
            IGenericSPSerializer? serializer = serializerResolver.GetSerialializerOrNull(null, entity, method);

            var (inputParameters, outputParameters) = parametersProvider.GetApiParameters(new Filter.ParameterInterceptorContext(entity, method, true));

            var context = new OperationResponseContext(entity, method, sPMiddlewareOptions.AlwaysWrapJson,
                    outputParameters);
            operation.Responses = GetDefaultResponse(codeConvention.GetResultTypeName(method),
                outputParameters.Length > 0 ? codeConvention.GetOutputObjectTypeName(method) : null,
                context);

            if (serializer != null)
                operation.Responses = serializer.ModifyResponses(operation.Responses, context);


            if (operationType != OperationType.Get && inputParameters.Any(p => p.WebApiName != null && !entity.ContainsPathParameter(p.WebApiName)))
            {
                if (operation.RequestBody == null) operation.RequestBody = new OpenApiRequestBody();
                operation.RequestBody.Required = true;
                operation.RequestBody.Description = "Parameters for " + method.SP.ToString();
                bool requireFormData = inputParameters.Any(p => p.RequiresFormData);
                operation.RequestBody.Content.Add(requireFormData ? "multipart/form-data" : "application /json", new OpenApiMediaType()
                {
                    Schema = new OpenApiSchema()
                    {
                        Reference = new OpenApiReference()
                        {
                            Type = ReferenceType.Schema,
                            Id = codeConvention.GetParameterObjectName(entity, method)
                        }
                    }
                });
            }
            if (operation.Parameters == null) operation.Parameters = new List<OpenApiParameter>();
            if (operationType == OperationType.Get)
            {
                foreach (var item in inputParameters.Where(p => p.WebApiName != null && !entity.ContainsPathParameter(p.WebApiName)))
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
            foreach (var item in inputParameters.Where(p => p.WebApiName != null && entity.ContainsPathParameter(p.WebApiName)))
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

#if NET47
        public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer)
        {
            var doc = new OpenApiDocument();
            Apply(doc, new DocumentFilterContext());
            var strW = new System.IO.StringWriter();
            doc.SerializeAsV2(new Microsoft.OpenApi.Writers.OpenApiJsonWriter(strW));
            string json = strW.ToString();
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.Ignore;
            var docOld = Newtonsoft.Json.JsonConvert.DeserializeObject<SwaggerDocument>(json, settings);
            foreach (var p in docOld.paths)
            {
                swaggerDoc.paths.Add(p.Key, p.Value);
            }
            foreach(var p in docOld.definitions)
            {
                swaggerDoc.definitions.Add(p.Key, p.Value);
            }
        }
#endif
    }
}

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
using System.Threading.Tasks;
using Kull.GenericBackend.Utils;
#if NETFX
using Swashbuckle.Swagger;
using Kull.MvcCompat;
using System.Web.Http.Description;
using IWebHostEnvironment = Kull.MvcCompat.IHostingEnvironment;
#else
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
#endif
#if NETSTD2
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#endif

namespace Kull.GenericBackend.SwaggerGeneration
{
#if NET48
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
        private readonly IWebHostEnvironment hostingEnvironment;

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
         ConfigProvider configProvider,
         IWebHostEnvironment hostingEnvironment)
        {
            this.codeConvention = codeConvention;
            this.hostingEnvironment = hostingEnvironment;
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

#if NET48
        public class DocumentFilterContext { }
#endif

        public async Task ApplyAsync(OpenApiDocument swaggerDoc, DocumentFilterContext context)
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
                        await WriteBodyPath(bodyOperation, ent, opType, method.Value);
                        openApiPathItem.Operations.Add(opType, bodyOperation);
                    }
                    swaggerDoc.Paths.Add(ent.GetUrl(this.sPMiddlewareOptions.Prefix, false), openApiPathItem);
                }
            }


            var allMethods = entities.SelectMany(e => e.Methods.Values);
            var resultSetPath = options.PersistResultSets ? (options.PersistedResultSetPath ?? System.IO.Path.Combine(hostingEnvironment.ContentRootPath, "ResultSets")) : null;
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
                    var dataToWrite = await sqlHelper.GetSPResultSet(dbConnection, method.SP, resultSetPath, method.ExecuteParameters!);
                    WriteJsonSchema(resultSchema, dataToWrite, namingMappingHandler, options.ResponseFieldsAreRequired,
                        options.UseSwagger2);
                    swaggerDoc.Components.Schemas.Add(typeName, resultSchema);
                }
            }

            foreach (var ent in entities)
            {
                foreach (var method in ent.Methods)
                {
                    var allParameters = (await parametersProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method.Value, true), method.Value.IgnoreParameters, dbConnection));
                    
                    var parameters = GetBodyOrQueryStringParameters(allParameters.inputParameters, ent, method.Value);
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
                        WriteJsonSchema(parameterSchema, parameters, options.ParameterFieldsAreRequired,
                                options.UseSwagger2);
                        swaggerDoc.Components.Schemas.Add(codeConvention.GetParameterObjectName(ent, method.Value),
                            parameterSchema);
                    }
                    if(allParameters.outputParameters.Any())
                    {
                        OpenApiSchema outputSchema = new OpenApiSchema();
                        WriteJsonSchema(outputSchema, allParameters.outputParameters, namingMappingHandler, true, options.UseSwagger2);
                        swaggerDoc.Components.Schemas.Add(codeConvention.GetOutputObjectTypeName(method.Value),
                            outputSchema);
                    }
                }
            }

        }
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            AsyncHelpers.RunSync(() => ApplyAsync(swaggerDoc, context));
        }


        private IReadOnlyCollection<Parameters.WebApiParameter> GetBodyOrQueryStringParameters(IEnumerable<WebApiParameter> inputParameters, Entity ent, Method method)
        {
            return inputParameters
                .Where(s => s.WebApiName != null && !ent.ContainsPathParameter(s.WebApiName))
                .ToArray();
        }

        private void WriteJsonSchema(OpenApiSchema parameterSchema, IReadOnlyCollection<Parameters.WebApiParameter> parameters,
            bool addRequired,
            bool forSwagger2)
        {
            parameterSchema.Type = "object";
            if(parameterSchema.Required == null && addRequired)
                parameterSchema.Required = new HashSet<string>();
            foreach (var item in parameters)
            {
                var prop = item.GetSchema();
                if (forSwagger2 && prop.Nullable)
                {
                    prop.AddExtension("x-nullable", new OpenApiBoolean(true));
                }
                parameterSchema.Properties.Add(
                     item.WebApiName,
                     prop);
                if (addRequired)
                    parameterSchema.Required!.Add(item.WebApiName);
                
            }
        }

        private static void WriteJsonSchema(OpenApiSchema schema,
            IEnumerable<SqlFieldDescription> props,
            NamingMappingHandler namingMappingHandler,
            bool addRequired,
            bool forSwagger2)
        {
            schema.Type = "object";
            var names = namingMappingHandler.GetNames(props.Select(p => p.Name))
                .GetEnumerator();
            if (schema.Xml == null) schema.Xml = new OpenApiXml();
            schema.Xml.Name = "tr";//it's always tr
            if (schema.Required == null && addRequired)
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
                if(forSwagger2 && prop.IsNullable)
                {
                    property.AddExtension("x-nullable", new OpenApiBoolean(true));
                }
                names.MoveNext();
                schema.Properties.Add(names.Current, property);
                if (addRequired)
                    schema.Required!.Add(names.Current);
            }
        }

        private static void WriteJsonSchema(OpenApiSchema schema,
            IEnumerable<OutputParameter> props,
            NamingMappingHandler namingMappingHandler,
            bool addRequired,
            bool forSwagger2)
        {
            schema.Type = "object";
            var names = namingMappingHandler.GetNames(props.Select(p => p.SqlName))
                .GetEnumerator();
            if (schema.Xml == null) schema.Xml = new OpenApiXml();
            schema.Xml.Name = "tr";//it's always tr
            if (schema.Required == null && addRequired)
                schema.Required = new HashSet<string>();
            foreach (var prop in props)
            {

                OpenApiSchema property = new OpenApiSchema();
                property.Type = prop.DbType.JsType;
                if (prop.DbType.JsFormat != null)
                {
                    property.Format = prop.DbType.JsFormat;
                }
                property.Nullable = true;
                if (forSwagger2)
                {
                    property.AddExtension("x-nullable", new OpenApiBoolean(true));
                }
                names.MoveNext();
                schema.Properties.Add(names.Current, property);
                if (addRequired)
                    schema.Required!.Add(names.Current);
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
                    Required = new HashSet<string>(new string[] { codeConvention.FirstResultKey }),
                    Properties = new Dictionary<string, OpenApiSchema>()
                   {
                       {  codeConvention.FirstResultKey, arrayOfResult },
                        { codeConvention.OtherResultsKey,  new OpenApiSchema()
                        {
                            Type="array",
                            Items = new OpenApiSchema()
                            {
                                Type="array",
                                Items = new OpenApiSchema()
                                {
                                    Type="object",
                                    AdditionalPropertiesAllowed=true
                                }
                            }
                        } }
                   }
                };
                if (outputObjectName != null)
                {
                    schema.Properties.Add(codeConvention.OutputParametersKey, new OpenApiSchema()
                    {
                        Reference = new OpenApiReference()
                        {
                            Type = ReferenceType.Schema,
                            Id = outputObjectName
                        }
                    });
                    schema.Required.Add(codeConvention.OutputParametersKey);
                }
            }
            response.Content.Add("application/json", new OpenApiMediaType()
            {
                Schema = schema

            });
            responses.Add("200", response);
            return responses;
        }


        private async Task WriteBodyPath(OpenApiOperation operation, Entity entity, OperationType operationType, Method method)
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

            var (inputParameters, outputParameters) = await parametersProvider.GetApiParameters(new Filter.ParameterInterceptorContext(entity, method, true), method.IgnoreParameters, dbConnection);

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

#if NET48
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
            if (docOld.paths != null)
            {
                foreach (var p in docOld.paths)
                {
                    swaggerDoc.paths.Add(p.Key, p.Value);
                }
            }
            if (docOld.definitions != null)
            {
                foreach (var p in docOld.definitions)
                {
                    swaggerDoc.definitions.Add(p.Key, p.Value);
                }
            }
        }
#endif
    }
}

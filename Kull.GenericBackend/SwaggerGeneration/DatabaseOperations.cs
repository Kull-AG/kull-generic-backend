using Kull.GenericBackend.Middleware;
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
using Unity;
using Swashbuckle.Swagger;
using Kull.MvcCompat;
using System.Web.Http.Description;
using IWebHostEnvironment = Kull.MvcCompat.IHostingEnvironment;
#else
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
#endif
#if NETSTD2
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#endif

namespace Kull.GenericBackend.SwaggerGeneration;
#if NET48
public class DatabaseOperationWrap : IDocumentFilter
{
    private readonly IUnityContainer? container = null;

    [Obsolete]
    public DatabaseOperationWrap()
    {
    }
    public DatabaseOperationWrap(IUnityContainer container)
    {
        this.container = container;
    }
    public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer)
    {
        if (container != null)
        {
            var realFilter = container.Resolve<IDocumentFilter>();
            realFilter.Apply(swaggerDoc, schemaRegistry, apiExplorer);
        }
        else
        {
            IDocumentFilter realFilter = (IDocumentFilter)System.Web.Mvc.DependencyResolver.Current.GetService(typeof(IDocumentFilter));
            realFilter.Apply(swaggerDoc, schemaRegistry, apiExplorer);
        }
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
    private readonly ParameterProvider parametersProvider;
    private readonly NamingMappingHandler namingMappingHandler;
    private readonly CodeConvention codeConvention;
    private readonly IWebHostEnvironment hostingEnvironment;
    private readonly ResponseDescriptor responseDescriptor;
    private readonly IServiceProvider serviceProvider;

    public DatabaseOperations(
     SPMiddlewareOptions sPMiddlewareOptions,
     SwaggerFromSPOptions options,
     SqlHelper sqlHelper,
     ILogger<DatabaseOperations> logger,
     ParameterProvider parametersProvider,
     SerializerResolver serializerResolver,
     NamingMappingHandler namingMappingHandler,
     CodeConvention codeConvention,
     ConfigProvider configProvider,
     IWebHostEnvironment hostingEnvironment,
     ResponseDescriptor responseDescriptor,
     IServiceProvider serviceProvider)
    {
        this.codeConvention = codeConvention;
        this.hostingEnvironment = hostingEnvironment;
        this.responseDescriptor = responseDescriptor;
        this.serviceProvider = serviceProvider;
        this.sPMiddlewareOptions = sPMiddlewareOptions;
        this.options = options;
        this.sqlHelper = sqlHelper;
        this.logger = logger;
        this.serializerResolver = serializerResolver;
        this.parametersProvider = parametersProvider;
        this.namingMappingHandler = namingMappingHandler;
        entities = configProvider.Entities;
    }

#if NET48
    public class DocumentFilterContext { }
#endif

    public async Task ApplyAsync(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        using var scope = serviceProvider.CreateScope();
        var dbConnection = scope.ServiceProvider.GetRequiredService<DbConnection>();
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
                    await WriteBodyPath(dbConnection, bodyOperation, ent, opType, method.Value);
                    openApiPathItem.Operations.Add(opType, bodyOperation);
                }
                swaggerDoc.Paths.Add(ent.GetUrl(this.sPMiddlewareOptions.Prefix, false), openApiPathItem);
            }
        }


        var allMethods = entities.SelectMany(e => e.Methods.Values);
        var resultSetPath = options.PersistResultSets ? (options.PersistedResultSetPath ?? System.IO.Path.Combine(hostingEnvironment.ContentRootPath, "ResultSets")) : null;
        foreach (var method in allMethods)
        {
            string typeName = method.ResultSchemaName ?? codeConvention.GetResultTypeName(method);
            if (swaggerDoc.Components.Schemas.ContainsKey(typeName))
            {
                logger.LogWarning($"Type {typeName} already exists in Components. Assuming it's the same");
            }
            else
            {
                OpenApiSchema resultSchema = new OpenApiSchema();
                try
                {
                    var dataToWrite = await sqlHelper.GetResultSet(dbConnection, method.DbObject, method.DbObjectType, resultSetPath, method.ExecuteParameters!);

                    if (method.IgnoreFields != null)
                    {
                        dataToWrite = dataToWrite.Where(dw => !method.IgnoreFields.Contains(dw.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
                    }
                    WriteJsonSchema(resultSchema, dataToWrite, namingMappingHandler, options.ResponseFieldsAreRequired,
                        options.UseSwagger2, jsonFields: method.JsonFields);
                }
                catch (Exception err)
                {
                    WriteJsonSchema(resultSchema, Array.Empty<SqlFieldDescription>(), namingMappingHandler, options.ResponseFieldsAreRequired,
                        options.UseSwagger2, jsonFields: method.JsonFields);
                    logger.LogError($"Error getting result set for {method.DbObject}. \r\n{err.ToString()}");
                }
                swaggerDoc.Components.Schemas.Add(typeName, resultSchema);
            }
        }

        foreach (var ent in entities)
        {
            foreach (var method in ent.Methods)
            {
                var allParameters = (await parametersProvider.GetApiParameters(new Filter.ParameterInterceptorContext(ent, method.Value, true), dbConnection));

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
                    swaggerDoc.Components.Schemas.Add(method.Value.ParameterSchemaName ?? codeConvention.GetParameterObjectName(ent, method.Value),
                        parameterSchema);
                }
                if (allParameters.outputParameters.Any())
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
        if (parameterSchema.Required == null && addRequired)
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
        bool forSwagger2,
        IReadOnlyCollection<string> jsonFields)
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
            if (jsonFields.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
            {
                property.Type = "object";
                // property.AdditionalPropertiesAllowed = true;// Not needed as per spec https://swagger.io/docs/specification/data-models/data-types/
            }
            else
            {
                property.Type = prop.DbType.JsType;
                if (prop.DbType.JsFormat != null)
                {
                    property.Format = prop.DbType.JsFormat;
                }
            }
            property.Nullable = prop.IsNullable;
            if (forSwagger2 && prop.IsNullable)
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




    private async Task WriteBodyPath(DbConnection dbConnection, OpenApiOperation operation, Entity entity, OperationType operationType, Method method)
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

        var (inputParameters, outputParameters) = await parametersProvider.GetApiParameters(new Filter.ParameterInterceptorContext(entity, method, true), dbConnection);

        var context = new OperationResponseContext(entity, method, sPMiddlewareOptions.AlwaysWrapJson,
                outputParameters,
                 method.ResultSchemaName ?? codeConvention.GetResultTypeName(method),
                outputParameters.Length > 0 ? codeConvention.GetOutputObjectTypeName(method) : null
                );
        operation.Responses = serializer?.GetResponseType(context) ?? /* in case of no serializer, we have to assume something */ responseDescriptor.GetDefaultResponse(context, false,
                context.OutputObjectTypeName != null || sPMiddlewareOptions.AlwaysWrapJson);


        if (operationType != OperationType.Get && inputParameters.Any(p => p.WebApiName != null && !entity.ContainsPathParameter(p.WebApiName)))
        {
            if (operation.RequestBody == null) operation.RequestBody = new OpenApiRequestBody();
            operation.RequestBody.Required = true;
            operation.RequestBody.Description = "Parameters for " + method.DbObject.ToString();
            bool requireFormData = inputParameters.Any(p => p.RequiresFormData);
            operation.RequestBody.Content.Add(requireFormData ? "multipart/form-data" : "application /json", new OpenApiMediaType()
            {
                Schema = new OpenApiSchema()
                {
                    Reference = new OpenApiReference()
                    {
                        Type = ReferenceType.Schema,
                        Id = method.ParameterSchemaName ?? codeConvention.GetParameterObjectName(entity, method)
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
        if (docOld != null && docOld.paths != null)
        {
            foreach (var p in docOld.paths)
            {
                swaggerDoc.paths.Add(p.Key, p.Value);
            }
        }
        if (docOld != null && docOld.definitions != null)
        {
            foreach (var p in docOld.definitions)
            {
                swaggerDoc.definitions.Add(p.Key, p.Value);
            }
        }
    }
#endif
}

using Kull.GenericBackend.SwaggerGeneration;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.Serialization
{
    public class ResponseDescriptor
    {
        private readonly CodeConvention codeConvention;

        public ResponseDescriptor(CodeConvention codeConvention)
        {
            this.codeConvention = codeConvention;
        }
        public virtual OpenApiResponses GetDefaultResponse(
                OperationResponseContext context,
                bool firstOnly=false)
        {

            OpenApiSchema schema;
            var outputObjectName = context.OutputObjectTypeName;
            var resultTypeName = context.ResultTypeName;
            if (outputObjectName != null || context.AlwaysWrapJson)
            {
                schema = GetWrappedSchema(resultTypeName, outputObjectName, firstOnly);
            }
            else
            {
                schema = firstOnly ? GetResultReference(resultTypeName) : GetArrayOfResult(resultTypeName);
            }

            OpenApiResponses responses = new OpenApiResponses();
            OpenApiResponse response = new OpenApiResponse();
            response.Description = $"OK"; // Required as per spec
            response.Content.Add("application/json", new OpenApiMediaType()
            {
                Schema = schema

            });
            responses.Add("200", response);
            return responses;
        }
        public virtual OpenApiSchema GetAdditionalItemsSchema() => new OpenApiSchema()
        {
            Type = "array",
            Items = new OpenApiSchema()
            {
                Type = "array",
                Items = new OpenApiSchema()
                {
                    Type = "object",
                    AdditionalPropertiesAllowed = true
                }
            }
        };

        public virtual OpenApiSchema GetWrappedSchema(
            string resultTypeName,
            string? outputObjectName,
                bool firstItemOnly)
        {

            OpenApiSchema schema = new OpenApiSchema()
            {
                Type = "object",
                Required = new HashSet<string>(new string[] { codeConvention.FirstResultKey }),
                Properties = new Dictionary<string, OpenApiSchema>()
                   {
                       {  codeConvention.FirstResultKey, firstItemOnly? GetResultReference(resultTypeName): GetArrayOfResult(resultTypeName) },
                        { codeConvention.OtherResultsKey,  GetAdditionalItemsSchema() }
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

            return schema;
        }

        public virtual OpenApiSchema GetResultReference(string resultTypeName) => new OpenApiSchema()
        {
            Reference = new OpenApiReference()
            {
                Type = ReferenceType.Schema,
                Id = resultTypeName
            }
        };

        public virtual OpenApiSchema GetArrayOfResult(string resultTypeName)
        {
            return new OpenApiSchema()
            {
                Type = "array",
                Xml = new OpenApiXml()
                {
                    Name = "table"
                },
                UniqueItems = false,
                Items = GetResultReference(resultTypeName)
            };
        }
    }
}

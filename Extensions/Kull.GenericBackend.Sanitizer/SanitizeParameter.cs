
using Ganss.Xss;
using Kull.Data;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.Parameters;
using Kull.GenericBackend.SwaggerGeneration;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.Sanitizer;

public class SanitizeParameter : WebApiParameter
{
    public override bool RequiresUserProvidedValue => true;

    private OpenApiSchema schema; // A bit a hack. we get the schema from the old parameter. should be string
    private HtmlSanitizer sanitizer;

    public SanitizeParameter(string? sqlName, string? webApiName, OpenApiSchema schema,
        HtmlSanitizer sanitizer) : base(sqlName, webApiName)
    {
        this.schema = schema;
        this.sanitizer = sanitizer;

    }

    public override OpenApiSchema GetSchema()
    {
        return schema;
    }

    public override object? GetValue(HttpContext? http, object? valueProvided, ApiParameterContext? parameterContext)
    {
        if (valueProvided == null) return null;
        if (valueProvided is string html)
        {
            var sanitized = sanitizer.Sanitize(html);

            return sanitized;
        }
        throw new InvalidOperationException("Expected a string");
    }
}
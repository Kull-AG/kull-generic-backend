#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.Parameters;

public class FileDescriptionParameter : WebApiParameter
{
    public bool Swagger2 { get; }

    public override bool RequiresFormData => true;

    // User cannot provide value anyway, SqlName is always null
    public override bool RequiresUserProvidedValue => false;

    public FileDescriptionParameter(string webApiName, bool swagger2) : base(null, webApiName)
    {
        this.Swagger2 = swagger2;
    }

    public override OpenApiSchema GetSchema()
    {
        return new OpenApiSchema()
        {
            Type = Swagger2 ? "file" : "string",
            Format = "binary"
        };
    }

    public override object? GetValue(HttpContext? http, object? valueProvided, ApiParameterContext? parameterContext)
    {
        return null;
    }
}

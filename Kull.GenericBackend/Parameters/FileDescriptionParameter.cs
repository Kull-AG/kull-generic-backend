#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.Parameters;

public class FileDescriptionParameter : WebApiParameter
{
    public override bool RequiresFormData => true;

    // User cannot provide value anyway, SqlName is always null
    public override bool RequiresUserProvidedValue => false;

    public FileDescriptionParameter(string webApiName) : base(null, webApiName)
    {

    }

    public override OpenApiSchema GetSchema()
    {
        return new OpenApiSchema()
        {
            Type = "file",
            Format = "binary"
        };
    }

    public override object? GetValue(HttpContext? http, object? valueProvided)
    {
        return null;
    }
}

using System;
#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.Parameters;

public class SystemParameter : WebApiParameter
{
    public override bool RequiresUserProvidedValue => false;

    private readonly Func<HttpContext, object?> getParameterValue;

    public SystemParameter(string sqlName,
          Func<HttpContext, object?> getParameterValue)
        : base(sqlName, null)
    {
        this.getParameterValue = getParameterValue ?? throw new ArgumentNullException(nameof(getParameterValue));
    }
    public override OpenApiSchema GetSchema()
    {
        return null!;
    }

    public override object? GetValue(HttpContext? http, object? valueProvided, ApiParameterContext? parameterContext)
    {
        if (http == null) return null;
        var vl = getParameterValue(http);
        return vl;
    }
}

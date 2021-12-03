#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;
using System.Collections.Generic;

namespace Kull.GenericBackend.Parameters;

/// <summary>
/// Base class for all parameters
/// </summary>
public abstract class WebApiParameter
{
    /// <summary>
    /// The name of the Parameter in SQL Server. Set this to null if you only want the parameter in OpenApi
    /// </summary>
    public string? SqlName { get; }

    /// <summary>
    /// The name of the parameter in the OpenApi Definition / Web API.
    /// Set this to null to get data otherwise (eg for interceptor)
    /// </summary>
    public string? WebApiName { get; }

    /// <summary>
    /// Set this to true to use Multipart/form-data
    /// </summary>
    public virtual bool RequiresFormData { get; } = false;

    public abstract object? GetValue(HttpContext? http, object? valueProvided);

    /// <summary>
    /// Set this to true if the value must be set by the user. if true and not set by the user, the db default will be used if true
    /// </summary>
    public abstract bool RequiresUserProvidedValue { get; }

    public WebApiParameter(string? sqlName, string? webApiName)
    {
        SqlName = sqlName;
        WebApiName = webApiName;
    }

    public virtual IEnumerable<WebApiParameter> GetRequiredTypes()
    {
        yield break;
    }

    public abstract OpenApiSchema GetSchema();
}

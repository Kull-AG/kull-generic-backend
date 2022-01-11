#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;
using System;
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

    private bool? hasOverWrittenGetValue1 = null;
    private bool? hasOverWrittenGetValue2 = null;

    // TODO for v3: Remove this and make GetValue with context abstract
    [Obsolete("Use overload with context")]
    public virtual object? GetValue(HttpContext? http, object? valueProvided)
    {
        hasOverWrittenGetValue1 = false;
        if(hasOverWrittenGetValue1==false && hasOverWrittenGetValue2 == false) { throw new InvalidOperationException("Must override GetValue"); }
        return GetValue(http, valueProvided, null);
    }

    public virtual object? GetValue(HttpContext? http, object? valueProvided, ApiParameterContext? parameterContext)
    {
        hasOverWrittenGetValue2 = false;
        if (hasOverWrittenGetValue1 == false && hasOverWrittenGetValue2 == false) { throw new InvalidOperationException("Must override GetValue"); }

#pragma warning disable CS0618 // Type or member is obsolete
        return GetValue(http, valueProvided);
#pragma warning restore CS0618 // Type or member is obsolete
    }

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


    public override string? ToString()
    {

        if (SqlName == WebApiName)
            return WebApiName!;
        if (SqlName == null)
            return "Api: " + WebApiName;
        if (WebApiName == null)
            return "Sql: " + SqlName;
        return base.ToString();
    }
}

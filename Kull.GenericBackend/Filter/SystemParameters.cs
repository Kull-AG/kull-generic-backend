#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.Filter;

public class SystemParameters : IParameterInterceptor
{

    static readonly Func<HttpContext, object?> noOpAccessor = (c) => null;

    public record struct DefaultImplementationData(Func<HttpContext, object?> ADLogin, 
        Func<HttpContext, object?> IPAddress,  Func<HttpContext, object?> UserAgent);

    public static readonly  DefaultImplementationData DefaultImplementations = new 
        (GetUserName, 
        #if NETFX 
        c=>c.Request.ServerVariables["REMOTE_ADDR"] 
        #else 
        c=>c.Connection?.RemoteIpAddress?.ToString()
        #endif
        ,
        c=>c.Request.Headers["User-Agent"].ToString()
        );

    private Dictionary<string, Func<HttpContext, object?>> getFns = new Dictionary<string, Func<HttpContext, object?>>(
        StringComparer.CurrentCultureIgnoreCase)
        {
            { "NTLogin",DefaultImplementations.ADLogin },
            { "ADLogin",DefaultImplementations.ADLogin},
            { "IPAddress", DefaultImplementations.IPAddress},
            { "UserAgent", DefaultImplementations.UserAgent}
        };



    private static string? GetUserName(HttpContext context)
    {
        return context.User?.Identity?.Name;
    }

    public string[] GetSystemParameters()
    {
        return getFns.Keys.ToArray();
    }

    protected bool TryGetValueAccessor(Data.DBObjectName spname, string parameterName, out Func<HttpContext, object?> valueAccessor)
    {
        if (getFns.TryGetValue(parameterName, out var acc))
        {
            valueAccessor = acc;
            return true;
        }
        foreach (var key in getFns.Keys)
        {
            if (key.Contains(".")) // No dot -> must be a simple name and should have been matched already
            {
                string dbNamePart = key.Substring(0, key.LastIndexOf("."));
                string paramPart = key.Substring(key.LastIndexOf(".") + 1);
                if (spname == dbNamePart && paramPart.Equals(parameterName, StringComparison.CurrentCultureIgnoreCase))
                {
                    valueAccessor = getFns[key];
                    return true;
                }
            }
        }
        valueAccessor = noOpAccessor;
        return false;
    }


    /// <summary>
    /// Add a custom system paramters
    /// </summary>
    /// <param name="name"></param>
    /// <param name="valueAccessor"></param>
    public void AddSystemParameter(string name, Func<HttpContext, object?> valueAccessor)
    {
        this.getFns.Add(name, valueAccessor);
    }

    /// <summary>
    /// Remove all current system parameters
    /// </summary>
    public void Clear()
    {
        this.getFns.Clear();
    }

    void IParameterInterceptor.Intercept(ICollection<Parameters.WebApiParameter> apiParams, ParameterInterceptorContext parameterInterceptorContext)
    {
        List<Parameters.WebApiParameter> toRemove = new List<Parameters.WebApiParameter>();
        List<Parameters.WebApiParameter> toAdd = new List<Parameters.WebApiParameter>();
        foreach (var param in apiParams)
        {
            if (param.SqlName != null && TryGetValueAccessor(parameterInterceptorContext.Method.DbObject, param.SqlName, out var valueAccessor))
            {
                toRemove.Add(param);
                toAdd.Add(new Parameters.SystemParameter(param.SqlName,
                        valueAccessor));
            }
        }
        foreach (var i in toRemove) apiParams.Remove(i);
        foreach (var a in toAdd) apiParams.Add(a);
    }


}

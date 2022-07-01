using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.Middleware;

public class SPMiddlewareOptions
{
    public string Prefix { get; set; } = "/api/";

    /// <summary>
    /// The Encoding for the Body, defaults to UTF8 without BOM
    /// </summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);

    /// <summary>
    /// Requires user to be authenticated. True since 2.0
    /// </summary>
    public bool RequireAuthenticated { get; set; } = true;

#if NEWTONSOFTJSON
    /// <summary>
    /// Naming strategy for properties etc
    /// </summary>
    public Newtonsoft.Json.Serialization.NamingStrategy NamingStrategy { get; set; } = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy();
#else

    /// <summary>
    /// Naming strategy for properties etc
    /// </summary>
    public System.Text.Json.JsonNamingPolicy NamingStrategy { get; set; } = System.Text.Json.JsonNamingPolicy.CamelCase;
#endif

    /// <summary>
    /// Set this to true to always wrap your result in an object
    /// This prevents certain CORS Attacks for GET Requests
    /// </summary>
    public bool AlwaysWrapJson { get; set; } = false;

    /// <summary>
    /// Asp.Net Core Policies for the Requests
    /// </summary>
    public IReadOnlyCollection<string> Policies { get; init; } = Array.Empty<string>();
}

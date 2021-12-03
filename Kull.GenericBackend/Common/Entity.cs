using System;
using System.Collections.Generic;
using System.Linq;
#if NEWTONSOFTJSON
using Newtonsoft.Json.Serialization;
#endif
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Config;

namespace Kull.GenericBackend.Common;

/// <summary>
/// A class representing a REST Resource / a Database Entity
/// </summary>
public class Entity
{
    /// <summary>
    /// Url Parts as configured in appsettings.json
    /// Eg, for /Cases/{CaseId:int}/Brand the parts
    /// are ["Cases", "{CaseId:int}", "Brand"]
    /// </summary>
    private readonly string[] UrlParts;

    /// <summary>
    /// A map containing all methods of this entity
    /// </summary>
    public IReadOnlyDictionary<OperationType, Method> Methods { get; }

    /// <summary>
    /// The tag for Open Api
    /// </summary>
    public string? Tag { get; }

    private IReadOnlyDictionary<string, object?> restParameters;

    /// <summary>
    /// Gets the name and the type of a {} Template
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private (string name, string? type) ParseTemplatePart(string input)
    {
#if NEWTONSOFTJSON
        CamelCaseNamingStrategy strat = new CamelCaseNamingStrategy();
        if (input.Contains(":"))
        {
            return (strat.GetPropertyName(input.Substring(0, input.IndexOf(":")), false),
                input.Substring(input.IndexOf(":") + 1));
        }
        return (strat.GetPropertyName(input, false), null);
#else
        var strat = System.Text.Json.JsonNamingPolicy.CamelCase;
        if (input.Contains(":"))
        {
            return (strat.ConvertName(input.Substring(0, input.IndexOf(":"))),
                input.Substring(input.IndexOf(":") + 1));
        }
        return (strat.ConvertName(input), null);
#endif

    }

    /// <summary>
    /// Gets the names and the types of all Path parameters
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<(bool isParameterPart, string name, string? type)> GetUrlParts()
    {
        return UrlParts
            .Select(up => new { isParameterPart = up.StartsWith("{") && up.EndsWith("}"), part = up })
            .Select(s => (s.isParameterPart,
                s.isParameterPart ? ParseTemplatePart(s.part.Substring(1, s.part.Length - 2)).name : s.part,
                s.isParameterPart ? ParseTemplatePart(s.part.Substring(1, s.part.Length - 2)).type : s.part
                )
                )
            .ToArray();
    }

    /// <summary>
    /// Gets the names and the types of all Path parameters
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<(string name, string? type)> GetPathParameters()
    {
        return UrlParts
            .Where(s => s.StartsWith("{") && s.EndsWith("}"))
            .Select(s => ParseTemplatePart(s.Substring(1, s.Length - 2)))
            .ToArray();
    }

    public Entity(string urlTemplate, IReadOnlyDictionary<OperationType, Method> methods)
        : this(urlTemplate, methods, null, null)
    {

    }

    internal Entity(string urlTemplate, IReadOnlyDictionary<OperationType, Method> methods, string? tag,
            IReadOnlyDictionary<string, object?>? restParameters)
    {
        UrlParts = urlTemplate.Replace("|", ":").Split('/').Select(s => s.Trim()).ToArray();
        Methods = methods;
        Tag = tag;
        this.restParameters = restParameters ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Use for extension to get additional config values not in this object
    /// Returns null if not found
    /// </summary>
    /// <typeparam name="T">The expected type</typeparam>
    /// <param name="name">The name of the parameter</param>
    /// <returns></returns>
    public T GetAdditionalConfigValue<T>(string name) => restParameters.GetValue<T>(name);

    public Method GetMethod(string httpMethod)
    {
        if (!Enum.TryParse(httpMethod, true, out OperationType operationType))
        {
            throw new ArgumentException("Key must be a Http Method");
        }
        return Methods[operationType];
    }

    internal static Entity GetFromConfig(string key, object value)
    {
        var childConfig = (IReadOnlyDictionary<string, object?>)value;
        return new Entity(key, childConfig.Where(c => !c.Key.Equals("Config", StringComparison.CurrentCultureIgnoreCase))
                .Select(s => Method.GetFromConfig(s.Key, s.Value!))
                .ToDictionary(s => s.HttpMethod, s => s),
                childConfig.GetValue<IReadOnlyDictionary<string, object?>>("Config")?.GetValue<string>("Tag"),
                childConfig
                );
    }


    public string GetUrl(string prefix, bool withTemplateType)
    {
        if (!withTemplateType)
        {
            return string.Join("/", new string[] { prefix }
                .Concat(
                    UrlParts.Select(up =>
                        up.StartsWith("{") ?
                            "{" + ParseTemplatePart(up.Substring(1, up.Length - 2)).name + "}" :
                            up
                        )
                ).ToArray()).Replace("//", "/");
        }
        return string.Join("/", new string[] { prefix }.Concat(UrlParts).ToArray()).Replace("//", "/");
    }

    private string[]? pathParameters;

    /// <summary>
    /// Gets a boolean wheter this path does contains template with the given parameter name
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <returns></returns>
    public bool ContainsPathParameter(string name)
    {
        if (name == null) return false;
        pathParameters = pathParameters ?? GetPathParameters().Select(s => s.name.ToLower()).ToArray();
        return pathParameters.Contains(name.ToLower());

    }

    /// <summary>
    /// Gets the url with templates. Mainly for debugging
    /// </summary>
    /// <returns>the url</returns>
    public override string ToString()
    {
        return GetUrl("", false);
    }
}



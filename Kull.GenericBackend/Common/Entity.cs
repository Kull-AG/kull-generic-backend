using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Serialization;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Config;

namespace Kull.GenericBackend.Common
{

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
        public IDictionary<OperationType, Method> Methods { get; }

        /// <summary>
        /// The tag for Open Api
        /// </summary>
        public string? Tag { get; }


        /// <summary>
        /// Gets the name and the type of a {} Template
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private (string name, string? type) ParseTemplatePart(string input)
        {
            CamelCaseNamingStrategy strat = new CamelCaseNamingStrategy();
            if (input.Contains(":"))
            {
                return (strat.GetPropertyName(input.Substring(0, input.IndexOf(":")), false),
                    input.Substring(input.IndexOf(":") + 1));
            }
            return (strat.GetPropertyName(input, false), null);
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

        public Entity(string urlTemplate, IDictionary<OperationType, Method> methods)
            : this(urlTemplate, methods, null)
        {

        }

        internal Entity(string urlTemplate, IDictionary<OperationType, Method> methods, string? tag)
        {
            UrlParts = urlTemplate.Replace("|", ":").Split('/').Select(s => s.Trim()).ToArray();
            Methods = methods;
            Tag = tag;
        }

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
            var childConfig = (IDictionary<string, object?>)value;
            return new Entity(key, childConfig.Where(c => !c.Key.Equals("Config", StringComparison.CurrentCultureIgnoreCase))
                    .Select(s => Method.GetFromConfig(s.Key, s.Value!))
                    .ToDictionary(s => s.HttpMethod, s => s),
                    childConfig.GetValue<IDictionary<string, object?>>("Config")?.GetValue<string>("Tag")
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

}



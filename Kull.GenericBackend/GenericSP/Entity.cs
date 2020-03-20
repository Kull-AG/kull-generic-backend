using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Serialization;

namespace Kull.GenericBackend.GenericSP
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
        public readonly string[] UrlParts;


        public readonly IDictionary<string, Method> Methods;

        /// <summary>
        /// A string to be used for Representation in URLs or Methods
        /// Eg for /Cases/{CaseId|int}/Brand returns GetCasesBy
        /// </summary>
        /// <returns></returns>
        public string GetDisplayString()
        {
            List<string?> result = new List<string?>();
            bool lastWasBy = false;
            
            foreach (var part in UrlParts)
            {
                if (!part.StartsWith("{"))
                {
                    lastWasBy = false;
                    result.Add(part);
                }
                else
                {
                    string name = ParseTemplatePart(part.Substring(1, part.Length - 2)).name;

                    if (!lastWasBy)
                    {
                        lastWasBy = true;
                        string? lastUrlPart = result.LastOrDefault();
                        
                        string? entnameprm = name.EndsWith("Id") ? name.Substring(0, name.Length - "Id".Length) : null;
                        string? entnamepart = lastUrlPart != null && lastUrlPart.EndsWith("s") ?
                            lastUrlPart.Substring(0, lastUrlPart.Length - "s".Length) : null;
                        if (entnameprm != null &&
                            entnameprm.Equals(entnamepart, StringComparison.CurrentCultureIgnoreCase))
                        {
                            result.RemoveAt(result.Count - 1);
                            result.Add(entnamepart);
                            continue;
                        }
                        else
                        {
                            result.Add("By");
                        }
                    }
                    name = name[0].ToString().ToUpper() + name.Substring(1);
                    result .Add(name);
                }
            }
            return string.Join("", result);
        }

       
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
        public IReadOnlyCollection<(string name, string? type)> GetPathParameters()
        {
            return this.UrlParts
                .Where(s => s.StartsWith("{") && s.EndsWith("}"))
                .Select(s => ParseTemplatePart(s.Substring(1, s.Length - 2)))
                .ToArray();
        }

        public Entity(string urlTemplate, IDictionary<string, Method> methods)
        {
            this.UrlParts = urlTemplate.Replace("|", ":").Split('/').Select(s => s.Trim()).ToArray();
            this.Methods = methods;
        }

        public static Entity GetFromSection(IConfigurationSection section)
        {
            return new Entity(section.Key, section.GetChildren()
                    .Select(s => Method.GetFromSection(s))
                    .ToDictionary(s => s.HttpMethod, s => s, StringComparer.CurrentCultureIgnoreCase));
        }


        public string GetUrl(string prefix, bool withTemplateType)
        {
            if(!withTemplateType)
            {
                return string.Join("/", new string[] { prefix }
                    .Concat(
                        this.UrlParts.Select(up => 
                            up.StartsWith("{") ? 
                                "{" + ParseTemplatePart(up.Substring(1, up.Length-2)).name + "}":
                                up
                            )
                    ).ToArray()).Replace("//", "/");
            }
            return string.Join("/", new string[] { prefix }.Concat(this.UrlParts).ToArray()).Replace("//", "/");
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
            pathParameters = pathParameters ?? this.GetPathParameters().Select(s => s.name.ToLower()).ToArray();
            return pathParameters.Contains(name.ToLower());

        }
    }

}



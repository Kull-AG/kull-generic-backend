using Newtonsoft.Json.Serialization;
using System.Text;

namespace Kull.GenericBackend.GenericSP
{
    public class SPMiddlewareOptions
    {
        public string Prefix { get; set; } = "/api/";

        /// <summary>
        /// The Encoding for the Body, defaults to UTF8 without BOM
        /// </summary>
        public Encoding Encoding { get; set; } = new UTF8Encoding(false);

        public bool RequireAuthenticated { get; set; } = false;

        /// <summary>
        /// Naming strategy for properties etc
        /// </summary>
        public NamingStrategy NamingStrategy { get; set; } = new CamelCaseNamingStrategy();

        /// <summary>
        /// Set this to true to always wrap your result in an object
        /// This prevents certain CORS Attacks for GET Requests
        /// </summary>
        public bool AlwaysWrapJson { get; set; } = false;
    }
}

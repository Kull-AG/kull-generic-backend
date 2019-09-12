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
    }
}

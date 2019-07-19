namespace Kull.GenericBackend.GenericSP
{
    public class SPMiddlewareOptions
    {
        public string Prefix { get; set; } = "/api/";

        public bool RequireAuthenticated { get; set; } = false;
    }
}

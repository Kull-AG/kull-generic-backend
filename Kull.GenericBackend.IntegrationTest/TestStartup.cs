using Kull.GenericBackend.GenericSP;

namespace Kull.GenericBackend.IntegrationTest
{
    public class TestStartup : TestStartupBase
    {
        protected override bool UseSwaggerV2 => true;

        protected override void ConfigureMiddleware(SPMiddlewareOptions options)
        {
            base.ConfigureMiddleware(options);
            options.AlwaysWrapJson = false;
        }
    }
}

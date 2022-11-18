
using Ganss.Xss;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kull.GenericBackend.Sanitizer;

public static class GenericBackendExtension

{
    public static Kull.GenericBackend.GenericBackendBuilder AddSanitizer(this Kull.GenericBackend.GenericBackendBuilder builder)
    {
        IServiceCollection serv = builder.Services;
        serv.TryAddSingleton<HtmlSanitizer>();
        builder.AddParameterInterceptor<SanitizerParameterInterceptor>();
        return builder;
    }
}
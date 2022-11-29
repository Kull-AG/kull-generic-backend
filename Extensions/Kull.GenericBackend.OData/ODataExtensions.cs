
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kull.GenericBackend.OData;

public static class ODataExtensions

{
    public static Kull.GenericBackend.GenericBackendBuilder AddOData(this Kull.GenericBackend.GenericBackendBuilder builder)
    {
        IServiceCollection serv = builder.Services;
        serv.AddTransient<CommandPreparationOData>();
        serv.Replace(new ServiceDescriptor(typeof(Kull.GenericBackend.Execution.CommandPreparation),
            (sp) => (Kull.GenericBackend.Execution.CommandPreparation)sp.GetRequiredService<CommandPreparationOData>(),
            ServiceLifetime.Transient
          ));
        return builder;
    }
    public static void AddOData(this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        options.DocumentFilter<ODataFilters>();

    }
}
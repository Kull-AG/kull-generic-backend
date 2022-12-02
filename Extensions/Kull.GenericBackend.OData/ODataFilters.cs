using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kull.GenericBackend.OData;

public class ODataFilters : IDocumentFilter
{
    private readonly ODataOptions options;
    public ODataFilters(ODataOptions options)
    {
        this.options = options;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var path in swaggerDoc.Paths)
        {
            foreach (var op in path.Value.Operations)
            {
                if (op.Value.Extensions.TryGetValue("x-dbobject-type", out var ext) && ext is OpenApiString str && str.Value == Kull.DatabaseMetadata.DBObjectType.TableOrView.ToString())
                {
                    foreach (var odp in CommandPreparationOData.ODataPatermers)
                    {
                        if (odp != "expand" && odp != "lambda") // Not yet supported
                        {
                            var prm = new OpenApiParameter()
                            {
                                Name = "$" + odp,
                                Schema = new OpenApiSchema()
                                {
                                    Type =
                                        odp == "top" || odp == "skip" ? "integer" :
                                         "string",
                                },
                                Description = "see https://github.com/DynamicODataToSQL/DynamicODataToSQL",
                                In = ParameterLocation.Query,

                            };
                            if (odp == "top")
                            {
                                prm.Schema.Minimum = 1;
                                if (options.DefaultTop != -1)
                                    prm.Schema.Default = new OpenApiInteger(options.DefaultTop);
                                if (options.MaxTop != -1)
                                    prm.Schema.Maximum = options.MaxTop;
                            }
                            op.Value.Parameters.Add(prm);
                        }
                    }
                }
            }
        }
    }
}
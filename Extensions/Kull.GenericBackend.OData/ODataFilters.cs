using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kull.GenericBackend.OData;

public class ODataFilters : IDocumentFilter
{
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
                        op.Value.Parameters.Add(new OpenApiParameter()
                        {
                            Name = "$" + odp,
                            Schema = new OpenApiSchema()
                            {
                                Type = "string"
                            },
                            Description = "see https://github.com/DynamicODataToSQL/DynamicODataToSQL",
                            In = ParameterLocation.Query
                        });
                    }
                }
            }
        }
    }
}
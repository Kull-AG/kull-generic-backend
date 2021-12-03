namespace Kull.GenericBackend.SwaggerGeneration;

public class SwaggerFromSPOptions
{
    public string Path { get; set; } = "/swagger/v1/swagger.json";

    public bool PersistResultSets { get; set; } = true;

    public string? PersistedResultSetPath { get; set; } = null;

    /// <summary>
    /// True to add all fields of the response to the required array
    /// </summary>
    public bool ResponseFieldsAreRequired { get; set; } = true;

    /// <summary>
    /// True to add all fields of the parameter to the required array
    /// </summary>
    public bool ParameterFieldsAreRequired { get; set; } = false;

    /// <summary>
    /// Uses x-nullable for nullability
    /// </summary>
    public bool UseSwagger2 { get; set; } = false;
}

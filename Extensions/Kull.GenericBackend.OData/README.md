# Kull.GenericBackend.OData

This is a simple Demo Extension for Kull.GenericBackend, thats extending Query Execution by supporting OData Filters powered by [DynamicODataToSQL](https://github.com/DynamicODataToSQL/DynamicODataToSQL)

Use it by using Views or Table Valued Functions in the config:

```json
"EntityName": {
      "POST": {
        "VIEW": "dbo.AnyView"
      }
    }
```

And configure both Generic Backend and Swagger correctly:

```csharp
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Your API", Version = "v1" });
    c.AddGenericBackend();
    c.AddOData(); // Make sure to add this after Generic Backend and that Generic Backend has at least v2.5.4
});
...
services.AddGenericBackend().AddOData();
```

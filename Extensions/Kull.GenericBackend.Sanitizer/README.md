# Kull.GenericBackend.Sanitizer

This is a simple Demo Extension for Kull.GenericBackend, thats extending config and sanitizing HTML Parameters.

```json
"EntityName": {
      "POST": {
        "SP": "dbo.spUpdateDescription",
        "SanitizeHtmlParameters": ["Description"]
      }
    }
```

It uses the HtmlSanitizer Nuget Package. If you want to customize the Html Sanitizer options, just add an HtmlSanitizer as singleton before
calling `AddSanitizer` on the Kull.GenericBackend Builder

:

```csharp
services.AddSingleton<HtmlSanitizer>(_=>
{
    var sanitizer = new HtmlSanitizer();
    sanitizer.AllowedAttributes.Add("class");
    return sanitizer;
});
...
services.AddGenericBackend().AddSanitizer()
```

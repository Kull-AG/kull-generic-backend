---
name: kull-generic-backend
description: >
  Kull.GenericBackend — an ASP.NET Core middleware (Kull-AG/kull-generic-backend, NuGet
  package Kull.GenericBackend) that exposes SQL Server stored procedures, views, and
  table-valued functions as REST endpoints from a backendconfig.json file, with Swagger/
  OpenAPI generated automatically from sp_describe_first_result_set metadata — no
  controllers or DTOs to hand-write. Use this skill whenever the user mentions
  Kull.GenericBackend, GenericBackendBuilder, AddGenericBackend, UseGenericBackend,
  backendconfig.json/backendconfig.schema.json, or is wiring stored procedures straight
  to a REST API in an ASP.NET Core project. Also trigger when reviewing or writing a
  backendconfig.json entity map, adding System Parameters, IParameterInterceptor /
  IRequestInterceptor extensions, or debugging RAISERROR/THROW-based error codes coming
  back from this middleware.
---

# Kull.GenericBackend

Middleware for ASP.NET Core (source: github.com/Kull-AG/kull-generic-backend) that turns SQL
Server stored procedures, views, and table-valued functions into REST endpoints. You describe
the mapping once in `backendconfig.json`; the middleware handles routing, parameter binding,
JSON (de)serialization, and Swagger/OpenAPI generation (via `sp_describe_first_result_set` +
`INFORMATION_SCHEMA.parameters`) — there's no controller or DTO class to write per endpoint.

This skill covers the core `Kull.GenericBackend` package only. The `.OData` and `.Sanitizer`
extension packages are out of scope.

## Installation and wiring

```bash
dotnet add package Kull.GenericBackend
```

In `Startup.cs` / `Program.cs`, four things have to happen: register the backend, register a
`DbConnection`, wire Swagger, and add the middleware to the pipeline.

```csharp
using Kull.GenericBackend;

var services = builder.Services;
services.AddMvcCore().AddApiExplorer(); // or AddMvc()

services.AddGenericBackend()
    .ConfigureMiddleware(m =>
    {
        m.AlwaysWrapJson = true;        // recommended — avoids a CORS attack vector on bare-array GET responses
        m.RequireAuthenticated = true;  // default since 2.0; set false only for local dev
    })
    .ConfigureOpenApiGeneration(o => { /* PersistResultSets, ResponseFieldsAreRequired, ... */ })
    .AddFileSupport()      // enables file upload/download parameters
    // .AddXmlSupport()    // only if you need XML responses
    .AddSystemParameters(); // server-resolved parameters — see below

// The SQL Server provider factory usually needs manual registration
if (!DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out var _))
    DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);

// A DbConnection MUST be registered — the middleware resolves this per request
services.AddTransient(typeof(DbConnection), (s) =>
{
    var conf = s.GetRequiredService<IConfiguration>();
    return new Microsoft.Data.SqlClient.SqlConnection(conf["ConnectionStrings:DefaultConnection"]);
});

services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.AddGenericBackend(); // registers the IDocumentFilter that injects SP-derived paths
});

var app = builder.Build();
app.UseSwagger(o => o.SerializeAsV2 = false); // set true for older clients like ng-swagger-gen
app.UseRouting();
app.UseGenericBackend();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));
```

Because the Swagger integration is a document filter on Swashbuckle, hand-written controllers
and generic-backend endpoints coexist in the same `swagger.json` without conflict.

If the endpoint mapping style is used (`app.UseEndpoints(...)`) instead of the pipeline style
above, call `app.UseGenericBackend(endpoints)` inside `UseEndpoints`.

## backendconfig.json — mapping URLs to the database

This file is the entity map: keys are URL paths (no leading slash), values are per-HTTP-method
mappings to a stored procedure, view, or table-valued function.

```json
{
    "$schema": "https://raw.githubusercontent.com/Kull-AG/kull-generic-backend/master/backendconfig.schema.json",
    "Entities": {
        "Cases": {
            "Get": "api.spGetSomeCases"
        },
        "Cases/{CaseId|int}/Brands": {
            "Get": "api.spGetBrands",
            "Post": {
                "SP": "api.spAddUpdateBrands",
                "OperationId": "AddOrUpdateBrands",
                "IgnoreParameters": ["AParameterMyApiDoesNotCare"],
                "ExecuteParameters": { "SomeParam": 56 }
            }
        },
        "Sample": {
            "GET": { "View": "dbo.SomeView" }        // read-only, GET only
        },
        "SampleFunction": {
            "GET": { "Function": "dbo.FT_SomeTVF" }   // read-only, GET only
        }
    }
}
```

Key points, learned from the schema (`backendconfig.schema.json`) and the shipped test config:

- **HTTP verb keys are case-insensitive** (`Get`, `GET`, and `get` all work) — as are the object
  keys `SP` / `Sp` / `View` / `Function`. Pick one casing per project and stay consistent even
  though the parser tolerates all of them.
- **Shorthand vs. object form.** A bare string (`"Get": "api.spGetBrands"`) is shorthand for
  `{"SP": "api.spGetBrands"}`. Use the object form as soon as you need any other field below.
- **Route constraints use `|`, not `:`** — `{CaseId|int}` not `{CaseId:int}` — because `:` breaks
  `appsettings.json`/JSON key parsing. Same constraint vocabulary as ASP.NET Core routing
  otherwise (see the [route constraint reference](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-2.1#route-constraint-reference)).
- **Views and table-valued functions are read-only** — only `GET` is meaningful for `View`/
  `Function` entries; use `SP` for anything that mutates data.
- **`Config` at the entity level** sets a `Tag` (OpenAPI grouping) that applies to all methods
  under that URL unless a method overrides it with its own `Tag`.
- A **separate file** (rather than embedding `Entities` in `appsettings.json`) is recommended —
  it gets IntelliSense from `backendconfig.schema.json` and rarely needs to differ per
  environment.

### Per-method config fields (`MethodBase`)

| Field | Purpose |
|---|---|
| `OperationId` | OpenAPI operationId — must be unique across the whole API. Required once a URL/verb pair would otherwise generate a duplicate (e.g. reusing POST for a query because the URL would exceed ~2000 chars as a GET). |
| `OperationName` | `x-operation-name`, used by some client generators (e.g. ng-openapi-gen) for a name unique within a tag. |
| `Tag` | OpenAPI tag; overrides the entity-level `Config.Tag`. |
| `ResultType` | Force a content type: `File`, `Json`, `Xml`, `None`, or `First` (single-row result). Only needed when the `Accept` header doesn't already imply the right one. |
| `CommandTimeout` | Command timeout in seconds — raise for long-running procedures (e.g. search endpoints). |
| `IgnoreParameters` | SP parameters that exist in SQL but must not be exposed in the API — give them a SQL default and list them here. |
| `ExecuteParameters` | Literal values sent for parameters SQL Server didn't report metadata for. |
| `IgnoreFields` | Result columns to hide from the API response and docs (not supported by the XML serializer). |
| `ParameterSchemaName` / `ResultSchemaName` | Override the generated OpenAPI schema names for request/response. |
| `JsonFields` | Result columns that already contain a JSON string (e.g. built with `FOR JSON`) — serialized as nested JSON, not as an escaped string. The server does not validate this JSON; the procedure is responsible for it being valid. |
| `Policies` | ASP.NET Core authorization policy names required for this endpoint. |

## Writing the stored procedures

The middleware binds request data to SQL parameters and response columns to JSON fields
by **name** — there's no separate DTO layer, so the procedure signature effectively *is* the
API contract.

- **Parameter names become request fields.** For GET, they become query-string params; for
  POST/PUT/DELETE, JSON body fields (route parameters bind from the URL template regardless of
  verb). Naming policy defaults to camelCase (`SPMiddlewareOptions.NamingStrategy`), so
  `@SearchString` becomes `searchString` in the request.
- **Result columns become JSON response fields**, same naming policy applied in reverse.
- **Multiple result sets and output parameters** are supported (see the project wiki for the
  detailed shapes) — output parameters surface as additional response fields, not separate
  return values.
- **Table-valued parameters** work by declaring a table type in SQL; the caller sends a JSON
  array and the middleware materializes it into the TVP.
- **File upload parameters** follow a `<Name>_Content` / `<Name>_ContentType` / `<Name>_FileName`
  naming convention once `.AddFileSupport()` is enabled, e.g.:
  ```sql
  CREATE PROCEDURE dbo.spUploadImage
      @Image_Content varbinary(MAX),
      @Image_ContentType varchar(1000),
      @Image_FileName varchar(1000)
  AS ...
  ```
  For file **downloads**, set `"ResultType": "File"` on the method and return `ContentType`,
  `FileName`, and `Content` columns from the procedure.
- **JSON passthrough columns**: build a column with `... FOR JSON AUTO` and list it under
  `JsonFields` so it's emitted as a JSON object/array rather than a quoted string.

## Error handling

The client-facing error contract is driven entirely by T-SQL error codes — there is no
separate "throw an ApiException" layer to write against.

- **`RAISERROR('message', 16, 1, 1)`** (or `THROW`) surfaces `message` to the client as a
  structured error body. This is the way to reject invalid input or business-rule violations
  from inside a procedure.
- **To control the HTTP status code**, throw with an error number `50000 + <status code>`,
  where the status code is in `400..599`:
  ```sql
  THROW 50503, 'No access to this', 1;  -- responds with HTTP 503
  ```
  Numbers below `50000` (regular SQL Server errors, e.g. divide-by-zero) are *not* treated as
  user errors — they come back as a generic HTTP 500 with no error detail leaked to the client.
- **Errors mid-stream are not fully recoverable.** If a procedure has already started
  streaming a result set before it errors, the response is aborted and the client may receive
  truncated/invalid JSON — the status code can no longer be changed at that point. Validate and
  fail fast, before emitting any rows, whenever possible.

## System Parameters

System Parameters are SQL procedure parameters that the **server** resolves — the API consumer
never supplies them and they don't appear in the OpenAPI request schema. Enable them with
`.AddSystemParameters()` at startup; built in ones (matched case-insensitively against the SQL
parameter name) are:

| Parameter name | Resolves to |
|---|---|
| `NTLogin` / `ADLogin` | `HttpContext.User.Identity.Name` |
| `IPAddress` | The caller's remote IP |
| `UserAgent` | The `User-Agent` request header |

Add custom ones via the configure callback:

```csharp
services.AddGenericBackend()
    .AddSystemParameters(sp =>
    {
        sp.AddSystemParameter("TenantId", ctx => ctx.Items["TenantId"]);
    });
```

A procedure parameter is treated as a system parameter purely by **name match** — declare
`@IPAddress varchar(100)` in SQL and it's silently populated, no config file entry needed. See
the [System Parameters wiki page](https://github.com/Kull-AG/kull-generic-backend/wiki/System-Parameters)
for scoping a system parameter to one specific procedure (`Schema.Procedure.ParamName` keys).

## Extensibility

`GenericBackendBuilder` (returned by `.AddGenericBackend()`) exposes these hooks — reach for
them before working around the middleware from outside:

- **`AddParameterInterceptor<T>()`** where `T : IParameterInterceptor` — mutate the set of
  parameters bound for a call before execution (add/remove/replace `WebApiParameter`s). This is
  how `SystemParameters` and file-upload support are themselves implemented.
- **`AddRequestInterceptor<T>()`** where `T : IRequestInterceptor` — runs before the database is
  reached; return `(statusCode, content)` to short-circuit the request (e.g. custom auth checks
  beyond ASP.NET Core policies), or `null` to let it continue.
- **`AddRequestLogger<T>()`** — hook for request logging.
- **`AddSerializer<T>()`** where `T : IGenericSPSerializer` — plug in a response serializer
  beyond the built-in JSON/XML/File ones.

## Auth

- `SPMiddlewareOptions.RequireAuthenticated` defaults to `true` (since 2.0) — every generated
  endpoint requires an authenticated principal unless explicitly disabled for local dev.
- `SPMiddlewareOptions.Policies` (global) and the per-method `Policies` field in
  `backendconfig.json` map to standard ASP.NET Core authorization policies — register the
  policies normally via `services.AddAuthorization(...)`.

## Gotchas checklist

- Forgot to register a `DbConnection`? The middleware will fail at request time, not startup —
  double-check this first when everything 404s or 500s immediately.
- `AlwaysWrapJson = true` is the recommended default; a bare JSON array at the top level of a
  GET response is a known CORS/JSON-hijacking vector in older browsers.
- Entity URL keys never start with `/`.
- Direct view/table manipulation without a stored procedure isn't supported yet (tracked as
  future work) — every mutating endpoint needs a real procedure.

# ﻿﻿The Kull Generic Backend

This package allows Integration of a generic Stored Procedure-based Backend to Asp.Net MVC Core
It uses Swashbuckle, Version 5+

Nearly everything can be customized. It's designed to be extensible.

## Installation

[![NuGet Badge](https://buildstats.info/nuget/Kull.GenericBackend)](https://www.nuget.org/packages/Kull.GenericBackend/)

It's on Nuget: https://www.nuget.org/packages/Kull.GenericBackend/
Basically, just use:

```
dotnet add package Kull.GenericBackend
```

## Configuration

In Startup.cs, add the following services:

```csharp
using Kull.GenericBackend;
// ...

var services = builder.Services;
services.AddMvcCore().AddApiExplorer(); //Or AddMvc() depending on your needs
services.AddGenericBackend()
    .ConfigureMiddleware(m =>
    { // Set your options
        m.AlwaysWrapJson = true; // Recommended
        m.RequireAuthenticated = true; // default since 2.0. for local development, you might want to use false
    })
    .ConfigureOpenApiGeneration(o =>
    { // Set your options
    })
    .AddFileSupport()
    //.AddXmlSupport() if needed
    .AddSystemParameters(); // You probably want to configure these, see https://github.com/Kull-AG/kull-generic-backend/wiki/System-Parameters
	
// You might have to register your Provider Factory
if (!DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out var _))
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
	
// IMPORTANT: You have to inject a DbConnection somehow
services.AddTransient(typeof(DbConnection), (s) =>
{
    var conf = s.GetRequiredService<IConfiguration>();
    var constr = conf["ConnectionStrings:DefaultConnection"];
    return new Microsoft.Data.SqlClient.SqlConnection(constr);
});
services.AddSwaggerGen(c=> {
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
	c.AddGenericBackend();
});

//...
var app = builder.Build();
//...


app.UseSwagger(o =>
{
// Depending on your client, set this to true (eg, ng-swagger-gen)
    o.SerializeAsV2 = false;
});
app.UseRouting();
app.UseGenericBackend(); 

// If needed, Swagger UI, see https://github.com/domaindrivendev/Swashbuckle.AspNetCore
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

```
In a File called backendconfig.json, set the URI's:

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
                "IgnoreParameters":["AParameterMyApiDoesnotCare"],
                "ExecuteParameters": {
                    "ParamterName": "Set this object for strange cases where SQL Server does not return meta",
                    "AnotherParamter": "Be sure not to edit any data."
                }
            }
        },
        "Sample": {
            "GET": {
                "View": "dbo.[use a view if you want]" //  (currently readonly, get only)
            }
        },
        "SampleFunction": {
            "GET": {
                "Function": "dbo.[use a table valued function if you want]" //  (currently readonly, get only)
            }
        }
    }
}
```
You can place this in appsettings.json as well if you don't like a separate file. However having a separate file seems more appropriate here as it enables IntelliSense with VS Code. Also you usually do not have different configs per environment for this file.

In the "Entities" Section, the URL's are configured. Each entry correspondends to a URL.
Each URL can be accessed by multiple HTTP Methods. Common methods are:

- GET	for getting data, shoud NEVER update something
- POST	for adding/updating. Maybe misused for getting data if the url gets longer then 2000 chars otherwise), but then set the OperationId as seen above
- PUT	for updating data
- DELETE for deleting data

In the URL there can be route constraints as in MVC, however the | is used instead of :
because of limitations of appsettings.json
For a full documentation of allowed route constraints, please see [here](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-2.1#route-constraint-reference)

For best practices for defining a REST Api, see [here](https://docs.microsoft.com/en-us/azure/architecture/best-practices/api-design)

### Further features

For usage on Output Parameters, Table Valued Parameters and multiple Result Sets, view the wiki

## Main parts of the generic API

### Middleware for handling the requests

The main part of the project is the Middleware that handles stored procedures.
It is responsible for handling a request against some URL defined in the Entities Section,
as seen above.

### Middleware for Swagger

The other part is the middleware that is responsible for generating a swagger.json file
out of the information of the database. This is done by using the 
[sp_describe_first_result_set](https://docs.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-first-result-set-transact-sql?view=sql-server-2017)
and INFORMATION_SCHEMA.parameters. There is a separate Package for the Metadata: [Kull-AG/kull-databasemetadata](https://github.com/Kull-AG/kull-databasemetadata)

It works by adding an IDocumentFilter to Swashbuckle. Swashbuckle generates the swagger.json/openapi.json
This means you can mix this backend and your own Controllers and it will just work in the swagger.json.

### Special parameters

There are so called System Parameters, which are parameters defined in any Stored Procedure
which should be resolved by the Webserver and not by the Consumer of the API. 

More info here in the [wiki](https://github.com/Kull-AG/kull-generic-backend/wiki/System-Parameters)

# Error Handling 

In order to inform the client about an error, use `RAISERROR('SOMEERROR', 16,1,1);`
If there is another exception on the server, code 500 is sent whenever possible. However, 
when the error occurs during execution and not right at the start, the response will be aborted
and the status code cannot be guaranteed. In this case the result will be invalid JSON.

If you want to set the status code, use throw with code 50000 + Http Status Code between 400 and 599:
```sql
throw 50503, 'No access to this', 1 
```

# .Net 4.8

It works, but requires a lot of #if's and is not integration-tested. It's used in a number of projects though and does it's job.
See [wiki](https://github.com/Kull-AG/kull-generic-backend/wiki/Usage-with-MVC-5)

.Net Core is definitely the way to go, .Net 4.8 support is mainly to make porting to .Net Core easier.

# Performance

The performance should be very good as for the invocation very few Reflection is needed (if at all). Of course as this is IO-Bound/Database-Bound Code in practice the main bottleneck will be the database and the network. Meanwhile the Generic Backend should be as fast as it gets. 

# Possible futher development

- Direct manipulation of views without Stored Procedures (while staying secure)
- Support for other databases, eg. Sqlite for Testing (Might works already, tests pending)
- Even More Unit Tests

# ﻿﻿The Kull Generic Backend

This package allows Integration of a generic Stored Procedure-based Backend to Asp.Net MVC Core
It uses Swashbuckle, Version 5+

Nearly everything can be customized. It's designed to be extensible.

## Installation

[![NuGet Badge](https://buildstats.info/nuget/Kull.GenericBackend?includePreReleases=true)](https://www.nuget.org/packages/Kull.GenericBackend/)

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

public void ConfigureServices(IServiceCollection services)
{
		services.AddMvcCore().AddApiExplorer(); //Or AddMvc() depending on your needs
		services.AddGenericBackend()
            .ConfigureMiddleware(m =>
            { // Set your options
            })
            .ConfigureOpenApiGeneration(o =>
            { // Set your options
            })
            .AddFileSupport()
            .AddXmlSupport()
            .AddSystemParameters();
	
	// You might have to register your Provider Factory
	if (!DbProviderFactories.TryGetFactory("System.Data.SqlClient", out var _))
             DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
	
		// IMPORTANT: You have to inject a DbConnection somehow
        services.AddTransient(typeof(DbConnection), (s) =>
        {
            var conf = s.GetRequiredService<IConfiguration>();
            var constr = conf["ConnectionStrings:DefaultConnection"];
            return new System.Data.SqlClient.SqlConnection(constr);
        });
		services.AddSwaggerGen(c=> {
			c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
			c.AddGenericBackend();
		});
}
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
	app.UseSwagger(o =>
    {
	// Depending on your client, set this to true (eg, ng-swagger-gen)
        o.SerializeAsV2 = false;
    });
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
	app.UseGenericBackend(endpoints);
	endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    });

    // If needed, Swagger UI, see https://github.com/domaindrivendev/Swashbuckle.AspNetCore
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });

}
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
                "OperationId": "AddOrUpdateBrands"
            }
        }
    }
}
```
You can place this in appsettings.json as well if you don't like a separate file.

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

### Multi-value parameters

To use multiple values for a parameter, use a Sql Server User Defined Table Type
for the parameter.

```sql
-- Create the data type
CREATE TYPE dbo.IdNameType AS TABLE 
(
	Id bigint, 
	Name nvarchar(1000), 
    PRIMARY KEY (Id)
)
GO
-- Create SP
CREATE PROCEDURE dbo.spTestBackend
	@SomeId int,
	@Ids dbo.IdNameType readonly
AS
BEGIN
	-- ...
END
```

### Files

### File Upload

For uploading a file, name the parameters of your Stored Procedure with the following postfixes (at least two, content is required):

 - NAMEOFYOURFILEPARAM_Content
 - NAMEOFYOURFILEPARAM_ContentType
 - NAMEOFYOURFILEPARAM_FileName
 - NAMEOFYOURFILEPARAM_Length

 This will make the Generic Backend treat the SP as File Upload SP and therefore requiring the HTTP Request to be a Multipart/form-data Request.

### File Download

In your Settings File (appsettings.json or backendconfig.json) set the field "ResultType" to "File":

```json
    "FileDownload": {
      "GET": {
        "SP": "spGetFile",
        "ResultType": "File"
      }
    }
```

The SP must return a field called `Content` and a field called `ContentType`. It may return a field called `FileName` as well.

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

It works by adding an IDocumentFilter to Swashbuckle. Swashbuckle generates the swagger.json
This means you can mix this backend and your own Controllers and it will just work in the swagger.json.

### Special parameters

There are so called System Parameters, which are parameters defined in any Stored Procedure
which should be resolved by the Webserver and not by the Consumer of the API. 
Built-in are the following System Parameters:

- ADLogin & NTLogin: The Username in the HttpContext
- IPAddress: The IP Adress of the User
- UserAgent: The UserAgent of the Browser

If you would like to add something to this, or remove the default ones, you can do this in startup.cs:

```C#
    .AddSystemParameters(prms=>
    {
        prms.Clear();
        prms.AddSystemParameter("UserClaims", c => Newtonsoft.Json.JsonConvert.SerializeObject(c.User.Claims));
    });
```

# Error Handling 

In order to inform the client about an error, use `RAISERROR('SOMEERROR', 16,1,1);`
If there is another exception on the server, code 500 is sent whenever possible. However, 
when the error occurs during execution and not right at the start, the response will be aborted
and the status code cannot be guaranteed. In this case the result will be invalid JSON.

If you want to set the status code, use throw with code 50000 + Http Status Code between 400 and 599:
```sql
throw 50503, 'No access to this', 1 
```

# Extension Points

The whole tool is easily extensible by using the integrated Dependency Injection System of Asp.Net Core
There are two main things you can do:

- Write a IParameterInterceptor, as an example see [SystemParameters.cs](Kull.GenericBackend/Filter/SystemParameters.cs). This allows you to add or remove parameters
- Write a IGenericSPSerializer, as an example see [GenericSPFileSerializer.cs](Kull.GenericBackend/GenericSP/GenericSPFileSerializer.cs). This allows you to make a different result.
- Write a IRequestInterceptor, as an example see [IRequestInterceptor.cs](Kull.GenericBackend.IntegrationTest/TestRequestInterceptor.cs). This allows you to stop making a db call and return something else in special cases (eg for a permission check).
- Write a IResponseExceptionHandler, see [SqlServerExceptionHandler.cs](Kull.GenericBackend/Error/SqlServerExceptionHandler.cs). Use this to handle exceptions for the client or for logging

If you write an extension, it's best to do so using an Extension Method to [GenericBackendBuilder](Kull.GenericBackend/Builder/GenericBackendBuilder.cs)

# .Net 4.7

It works in theory, but requires a lot of #if's and is not integration-tested.
See [wiki](https://github.com/Kull-AG/kull-generic-backend/wiki/Usage-with-MVC-5)

# Possible futher development

- Direct manipulation of views without Stored Procedures (while staying secure)
- Support for other databases, eg Sqlite for Testing (Sqlite requires View support)
- Support for multiple Result Sets,  Return Codes and Output Parameters (would be realized through a IGenericSPSerializer, see above)
- Even More Unit Tests

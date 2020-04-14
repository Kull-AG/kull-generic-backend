# ﻿﻿The Kull Generic Backend

This package allows Integration of a generic Stored Procedure-based Backend to Asp.Net MVC Core
It uses Swashbuckle, Version 5+

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

public void ConfigureServices(IServiceCollection services)
{
		services.AddMvcCore().AddApiExplorer(); //Or AddMvc() depending on your needs
		services.AddGenericBackend(null, new Kull.GenericBackend.SwaggerGeneration.SwaggerFromSPOptions() {

		});
		
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
		// For compat with ng-swagger-gen on client. You can use ng-openapi-gen if set to false
        o.SerializeAsV2 = true;
    });
    app.UseMvc(routeBuilder=>
    {
		// The package relies on integrated routing of Asp.net MVC Core
        app.UseGenericBackend(routeBuilder);
    });
    // If needed, Swagger UI
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });

}
```
In appsettings.json, set the URI's:

```json
{
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

If you would like to add something to this, you can Subclass the SystemParameters class
and replace the default implementation by using Asp.Net Core Dependency Injection.

# Error Handling 

In order to inform the client about an error, use `RAISERROR('SOMEERROR', 16,1,1);`
If there is another exception on the server, code 500 is sent whenever possible. However, 
when the error occurs during execution and not right at the start, the response will be aborted
and the status code cannot be guaranteed. In this case the result will be invalid JSON.

# Possible futher development

- Direct manipulation of tables/views without Stored Procedures
- Support for other databases, eg Sqlite for Testing
- Support for multiple Result Sets,  Return Codes and Output Parameters
- More Unit Tests

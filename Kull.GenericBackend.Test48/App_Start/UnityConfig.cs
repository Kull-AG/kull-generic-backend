using Kull.DatabaseMetadata;
using Kull.GenericBackend;
using Kull.MvcCompat;
using Newtonsoft.Json.Serialization;
using System.Data.Common;
using Unity;


public class UnityConfig
{
    public static UnityContainer Container { get; } = new UnityContainer();

    public static void RegisterServices()
    {
        Container.AddMvcCompat();
        Container.AddKullDatabaseMetadata();
        Container.AddGenericBackend()
            .ConfigureMiddleware(m =>
            {
                    // In MVC 5 it usually was common to use PascalCase. DefaultNamingStrategy just leaves everything as is
                    m.NamingStrategy = new DefaultNamingStrategy();
            })
            .AddSystemParameters()
            .AddFileSupport();
        Container.AddTransient<DbConnection>((sp) =>
        {
            return Kull.Data.DatabaseUtils.GetConnectionFromConfig("TestConStr", System.Data.SqlClient.SqlClientFactory.Instance);
        });
    }
}

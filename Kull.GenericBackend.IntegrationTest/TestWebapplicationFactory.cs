using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Kull.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend.IntegrationTest
{
    public class TestWebApplicationFactory
        : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<TestStartup>
    {
        const int expectedVersion = 6; // sync this with sqlscript.sql
         
        static object setupObj = new object();
        private static void SetupDb(string dataPath, string constr)
        {
            lock (setupObj)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dataPath, "GenericBackendTest.mdf")))
                {
                    string testCommand = "SELECT VersionNr FrOM  dbo.TestDbVersion";
                    int version;
                    using (SqlConnection connection = new SqlConnection(constr))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandType = System.Data.CommandType.Text;
                        cmd.CommandText = testCommand;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            rdr.Read();
                            version = rdr.GetInt32(0);
                        }
                    }

                    if (version < expectedVersion)
                    {
                        using (SqlConnection connection = new SqlConnection(@"server=(localdb)\MSSQLLocalDB"))
                        {
                            connection.Open();
                            var cmdDropCon = new SqlCommand("ALTER DATABASE [GenericBackendTest] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", connection);
                            cmdDropCon.ExecuteNonQuery();
                            SqlCommand command = new SqlCommand("DROP DATABASE GenericBackendTest", connection);
                            command.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        return; // Everything ok
                    }
                }


                using (SqlConnection connection = new SqlConnection(@"server=(localdb)\MSSQLLocalDB"))
                {
                    connection.Open();

                    string sql = string.Format(@"
        CREATE DATABASE
            [GenericBackendTest]
        ON PRIMARY (
           NAME=GenericBackendTest,
           FILENAME = '{0}\GenericBackendTest.mdf'
        )
        LOG ON (
            NAME = GenericBackendTest_log,
            FILENAME = '{0}\GenericBackendTest.ldf'
        )",
                        dataPath
                    );

                    SqlCommand command = new SqlCommand(sql, connection);
                    command.ExecuteNonQuery();



                }

                var sqls = System.IO.File.ReadAllText(System.IO.Path.Combine(dataPath, "sqlscript.sql"))
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split("\nGO\n")
                    .Select(s => s.Replace("\n", Environment.NewLine));
                using (SqlConnection connection = new SqlConnection(constr))
                {
                    connection.Open();
                    foreach (var sql in sqls)
                    {
                        SqlCommand datacommand = new SqlCommand(sql, connection);
                        datacommand.ExecuteNonQuery();
                    }
                }
            }
        }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            return Microsoft.AspNetCore.WebHost.CreateDefaultBuilder()
                .UseStartup<TestStartup>();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {

            builder.ConfigureServices(services =>
            {
#if NETSTD2
                var hostEnv = (IHostingEnvironment)services.FirstOrDefault(f => f.ServiceType == typeof(IHostingEnvironment)).ImplementationInstance;
#else
                var hostEnv = (IWebHostEnvironment)services.FirstOrDefault(f => f.ServiceType == typeof(IWebHostEnvironment)).ImplementationInstance;
#endif
                var config = (IConfiguration)services.First(f => f.ServiceType == typeof(IConfiguration)).ImplementationInstance;
                // Not nice, but it seems as of .net core 3 this is required
                if (config == null)
                {
                    config = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", true, true)
                        .Build();
                }
                var constr = config["ConnectionStrings:DefaultConnection"];
                constr = constr.Replace("{{workdir}}", hostEnv.ContentRootPath);


                SetupDb(hostEnv.ContentRootPath, constr);

                // Build the service provider.
                var sp = services.BuildServiceProvider();
            });
        }
    }
}

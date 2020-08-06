using System.Data.SqlClient;
using System.Linq;
using System;

namespace Kull.GenericBackend.IntegrationTest.Utils
{

    public static class DatabaseUtils
    {
        const int expectedVersion = 9;

        static object setupObj = new object();
        public static void SetupDb(string dataPath, string constr)
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
                    .Replace("{{DbVersion}}", expectedVersion.ToString())
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


    }
}
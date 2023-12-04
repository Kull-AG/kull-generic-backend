using Microsoft.Data.SqlClient;
using System.Linq;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Kull.GenericBackend.IntegrationTest.Utils;

public static class DatabaseUtils
{
    const int expectedVersion = 28;

    static object setupObj = new object();
    public static void SetupDb(string dataPath, string constr)
    {
        lock (setupObj)
        {

            if (CheckIfMDFFileExists(System.IO.Path.Combine(dataPath, "GenericBackendTest.mdf")))
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
                    using (SqlConnection connection = new SqlConnection(@"Server=sql-server-test,1433;User ID=sa;Password=abcDEF123#;TrustServerCertificate=True;Encrypt=false;"))
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


            using (SqlConnection connection = new SqlConnection(@"Server=sql-server-test,1433;User ID=sa;Password=abcDEF123#;TrustServerCertificate=True;Encrypt=false"))
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
            var largeText = System.IO.File.ReadAllText(System.IO.Path.Combine(dataPath, "somelargetext.txt"));
            var sqls = System.IO.File.ReadAllText(System.IO.Path.Combine(dataPath, "sqlscript.sql"))
                .Replace("{{DbVersion}}", expectedVersion.ToString())
                .Replace("{{largetxt}}", largeText.Replace("'", "''"))
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split("\nGO\n")
                .Select(s => s.Replace("\n", Environment.NewLine));
            using (SqlConnection connection = new SqlConnection(constr))
            {
                connection.Open();
                foreach (var sql in sqls)
                {
                    if (sql.Trim().Length > 0)
                    {
                        SqlCommand datacommand = new SqlCommand(sql, connection);
                        datacommand.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    public static bool CheckIfMDFFileExists(string dataPath)
    {
        string testCommand = String.Format(@"
        DECLARE @result INT
        EXEC master.dbo.xp_fileexist '{0}', @result OUTPUT
        SELECT cast(@result as bit)
        ", dataPath);
        bool fileExists;
        using (SqlConnection connection = new SqlConnection(@"Server=sql-server-test,1433;User ID=sa;Password=abcDEF123#;TrustServerCertificate=True;Encrypt=false"))
        {
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = testCommand;
            using (var rdr = cmd.ExecuteReader())
            {
                rdr.Read();
                fileExists = rdr.GetBoolean(0);
            }
        }
        return fileExists;

    }


}

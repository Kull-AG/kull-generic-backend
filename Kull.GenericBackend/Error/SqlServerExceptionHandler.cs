using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Kull.GenericBackend.Error
{
    public class SqlServerExceptionHandler : IResponseExceptionHandler
    {
        // Numbers from here: https://docs.microsoft.com/en-us/sql/t-sql/language-elements/throw-transact-sql?view=sql-server-ver15#arguments
        public const int SQLServerUserErrorLowerBound = 50000;
        public const int SQLServerUserErrorUpperBound = 2147483647;

        

        public (int statusCode, System.Net.Http.HttpContent dataToDisplay)? GetContent(Exception exp)
        {

            if (exp is System.Data.SqlClient.SqlException err)
            {
                var errors = err.Errors.Cast<System.Data.SqlClient.SqlError>();
                if (!errors.All(e => e.Number >= SQLServerUserErrorLowerBound && e.Number <= SQLServerUserErrorUpperBound))
                {
                    return (500, new System.Net.Http.StringContent(""));
                }
                else
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(new SqlExceptionInfo(
                        errors.Select(s => s.Message).ToArray()
                    ));
                    return (400, new System.Net.Http.StringContent(json));

                }
            }
            return null;
        }

        public class SqlExceptionInfo
        {
            [Obsolete("Use for api only")]
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
            public SqlExceptionInfo()
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
            {

            }

            public SqlExceptionInfo(string[] errors)
            {
                this.Errors = errors;
            }

            public string[] Errors { get; set; }
        }
    }
}

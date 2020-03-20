using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Kull.GenericBackend.Error
{
    public class SqlServerExceptionHandler : IResponseExceptionHandler
    {
        public const int SQLServerUserError = 50000;

        public bool CanHandle(Exception err)
        {
            return err is System.Data.SqlClient.SqlException;
        }

        public (int statusCode, object? dataToDisplay) GetContent(Exception exp)
        {
            var err = (System.Data.SqlClient.SqlException)exp;
            var errors = err.Errors.Cast<System.Data.SqlClient.SqlError>();
            if (errors.Any(e => e.Number != SQLServerUserError))
            {
                return (500, null);
            }
            else
            {
                return (400, new SqlExceptionInfo(
                    errors.Select(s => s.Message).ToArray()
                ));

            }
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

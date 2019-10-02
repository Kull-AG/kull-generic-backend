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

        public (int statusCode, object dataToDisplay) GetContent(Exception exp)
        {
            var err = (System.Data.SqlClient.SqlException)exp;
            var errors = err.Errors.Cast<System.Data.SqlClient.SqlError>();
            if (errors.Any(e => e.Number != SQLServerUserError))
            {
                return (500, null);
            }
            else
            {
                return (400, new SqlExceptionInfo(){
                    Errors = errors.Select(s =>s.Message).ToArray()
                });

            }
        }

        public class SqlExceptionInfo
        {
            public SqlExceptionInfo()
            {
            }

            public string[] Errors { get; set; }
        }
    }
}

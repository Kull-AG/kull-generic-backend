using System;
using System.Linq;

namespace Kull.GenericBackend.Error;

public class SqlServerExceptionHandler : IResponseExceptionHandler
{
    // Numbers from here: https://docs.microsoft.com/en-us/sql/t-sql/language-elements/throw-transact-sql?view=sql-server-ver15#arguments
    public const int SQLServerUserErrorLowerBound = 50000;
    public const int SQLServerUserErrorUpperBound = 2147483647;



    public (int statusCode, System.Net.Http.HttpContent dataToDisplay)? GetContent(Exception exp, Func<object, System.Net.Http.HttpContent> format)
    {
        if (exp.GetType().FullName.EndsWith(".Data.SqlClient.SqlException"))// Use of reflection to avoid referencing ms sqlclient dll
        {
            var errorsProp = (System.Collections.ICollection)exp.GetType().GetProperty("Errors")!.GetValue(exp);
            var propType = errorsProp.Count > 0 ? errorsProp.Cast<object>().First().GetType() : null;
            var nrProp = propType?.GetProperty("Number");
            var messageProp = propType?.GetProperty("Message");
            var stateProp = propType?.GetProperty("State");
            var errors = errorsProp.Cast<object>().Select(e =>
            {
                return new
                {
                    Number = (int)nrProp!.GetValue(e),
                    Message = (string)messageProp!.GetValue(e),
                    State = (byte)stateProp!.GetValue(e)
                };
            });
            if (!errors.All(e => e.Number >= SQLServerUserErrorLowerBound && e.Number <= SQLServerUserErrorUpperBound))
            {
                return (500, format(new object()));
            }
            else
            {
                var content = format(new SqlExceptionInfo(
                    errors.Select(s => new SqlExceptionItem(s.State, s.Message)).ToArray()
                ));
                int responseCode = 400;
                responseCode = errors.FirstOrDefault(e => e.Number >= SQLServerUserErrorLowerBound + 400 && e.Number <= SQLServerUserErrorLowerBound + 599)?.Number - SQLServerUserErrorLowerBound

                    ?? responseCode;
                return (responseCode, content);

            }
        }
        return null;
    }

    public class SqlExceptionItem
    {
        public int State { get; }
        public string Message { get; }

        [Obsolete("Use for api only")]
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public SqlExceptionItem()
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {

        }
        public SqlExceptionItem(int state, string message)
        {
            this.State = state;
            this.Message = message;
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

        public SqlExceptionInfo(SqlExceptionItem[] errors)
        {
            this.Errors = errors;
        }

        public SqlExceptionItem[] Errors { get; }
    }
}

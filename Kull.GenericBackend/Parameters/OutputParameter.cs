using Kull.DatabaseMetadata;

namespace Kull.GenericBackend.Parameters
{
    /// <summary>
    /// Represents a parameter that returns a value (instead of providing one)
    /// </summary>
    public class OutputParameter
    {
        public SqlType DbType { get; }

        public string SqlName { get; }

        public OutputParameter(string name, SqlType sqlType)
        {
            this.SqlName = name;
            this.DbType = sqlType;
        }
    }
}

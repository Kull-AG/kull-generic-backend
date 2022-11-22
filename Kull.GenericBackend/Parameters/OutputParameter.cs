using System;
using System.Data.Common;
using Kull.DatabaseMetadata;
using System.Linq;

namespace Kull.GenericBackend.Parameters;

/// <summary>
/// Represents a parameter that returns a value (instead of providing one)
/// </summary>
public class OutputParameter
{
    public SqlType DbType { get; }

    public string SqlName { get; }

    public int? MaxLength { get; set; }

    public OutputParameter(string name, SqlType sqlType, int? maxLength)
    {
        this.SqlName = name;
        this.DbType = sqlType;
        this.MaxLength = maxLength;
    }

}

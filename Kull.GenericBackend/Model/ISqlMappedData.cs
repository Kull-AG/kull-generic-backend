using Kull.Data;

namespace Kull.GenericBackend.Model
{
    /// <summary>
    /// Represents a parameter to a procedure or a field in a table
    /// that is somehow mapped to a JSON-Property
    /// </summary>
    public interface ISqlMappedData
    {
        string SqlName { get; }

        string WebApiName { get; set; }

        SqlType DbType { get; }


        DBObjectName UserDefinedType { get; }
        bool IsNullable { get; }
    }
}
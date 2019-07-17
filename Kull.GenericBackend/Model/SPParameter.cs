using Kull.Data;
using System;
using System.Linq;

namespace Kull.GenericBackend.Model
{
    /// <summary>
    /// Represents a parameter for a Stored Proc
    /// </summary>
    public class SPParameter: ISqlMappedData
    {
        public string SqlName { get; }

        public string WebApiName { get; set; }

        public SqlType DbType { get; }

        public bool IsNullable => true; // A parameter is always nullable

        public DBObjectName UserDefinedType { get; }

        internal SPParameter(string prmName, string webApiName, string db_type,
                DBObjectName userDefinedType)
        {
            this.SqlName = prmName;
            this.WebApiName = webApiName;
            this.DbType = SqlType.GetSqlType(db_type);
            this.UserDefinedType = userDefinedType;
            if (this.SqlName.EndsWith("Json", StringComparison.CurrentCultureIgnoreCase)
                    && this.DbType.JsType == "string")
            {
                this.DbType = SqlType.GetSqlType("json");
            }
        }

        public (string name, string format) GetJSType()
        {
            (string name, string format) = (this.DbType.JsType, this.DbType.JsFormat);
            
            return (name, format);
        }

    }
}

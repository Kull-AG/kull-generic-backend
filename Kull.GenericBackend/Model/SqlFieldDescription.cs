using Kull.Data;
using Newtonsoft.Json.Linq;

namespace Kull.GenericBackend.Model
{
    /// <summary>
    /// A representation for a field in a table
    /// </summary>
    public class SqlFieldDescription: ISqlMappedData
    {
        public string Name { get; set; }
        
        public SqlType TypeName { get; set; }
        
        public bool IsNullable { get; set; }

        string ISqlMappedData.SqlName => Name;

        private string _webapiname;
        string ISqlMappedData.WebApiName { get { return _webapiname ?? Name; } set
            {
                _webapiname = value;
            }
        }

        SqlType ISqlMappedData.DbType => TypeName;

        bool ISqlMappedData.IsNullable => IsNullable;

        DBObjectName ISqlMappedData.UserDefinedType => null;

        public static SqlFieldDescription FromJObject(JObject jObject)
        {
            var obj = new SqlFieldDescription();
            obj.Name = jObject["name"]?.Value<string>() ?? jObject["Name"].Value<string>();
            obj.TypeName = SqlType.GetSqlType(jObject["system_type_name"]?.Value<string>() ?? jObject["TypeName"].Value<string>());
            obj.IsNullable = jObject["is_nullable"]?.Value<bool>() ?? jObject["IsNullable"].Value<bool>();
            return obj;
        }

        public JObject Serialize()
        {
            return new JObject(
                new JProperty("Name", this.Name),
                new JProperty("TypeName", this.TypeName.DbType),
                new JProperty("IsNullable", this.IsNullable));
        }

    }

}

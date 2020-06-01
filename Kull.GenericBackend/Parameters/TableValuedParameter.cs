using Kull.Data;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
#if NET47
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.Parameters
{
    public class TableValuedParameter : WebApiParameter
    {
        private readonly SqlFieldDescription[] fields;
        private readonly NamingMappingHandler namingMappingHandler;

        public DBObjectName UserDefinedType { get; }

        public TableValuedParameter(string webApiName,
              DBObjectName userDefinedType,
              SqlHelper sqlHelper,
              NamingMappingHandler namingMappingHandler
              ) : base(null, webApiName)
        {
            UserDefinedType = userDefinedType;
            this.namingMappingHandler = namingMappingHandler;
            this.fields = sqlHelper.GetTableTypeFields(userDefinedType);
        }

        public override OpenApiSchema GetSchema()
        {
            OpenApiSchema schema = new OpenApiSchema();
            schema.Type = "object";
            var names = namingMappingHandler
                .GetNames(fields.Select(f => f.Name))
                .GetEnumerator();

            foreach (var prop in fields)
            {

                OpenApiSchema property = new OpenApiSchema();
                property.Type = prop.DbType.JsType;
                if (prop.DbType.JsFormat != null)
                {
                    property.Format = prop.DbType.JsFormat;
                }
                property.Nullable = prop.IsNullable;
                names.MoveNext();
                schema.Properties.Add(names.Current, property);

            }
            return schema;
        }

        public override object? GetValue(HttpContext http, object? valueProvided)
        {

            System.Data.DataTable dt;
            dt = new System.Data.DataTable();
            foreach (var col in fields)
            {
                dt.Columns.Add(col.Name, col.DbType.NetType);
            }
            var rowData = (IEnumerable<IDictionary<string, object>>?)valueProvided;
            if(rowData == null)
            {
                return null;
            }
            var names = namingMappingHandler
                .GetNames(fields.Select(f => f.Name))
                .ToArray();
            foreach (var row in rowData)
            {
                object?[] values = new object[dt.Columns.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    var colWebApiName = names[i];
                    values[i] = row.ContainsKey(colWebApiName) ?
                        row[colWebApiName] : null;
                }
                dt.Rows.Add(values);
            }
            return dt;

        }
    }

}

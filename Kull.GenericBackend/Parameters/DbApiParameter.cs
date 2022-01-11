using Kull.Data;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.Utils;
#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;
#if NEWTONSOFTJSON
using Newtonsoft.Json;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Kull.GenericBackend.Parameters;

public class DbApiParameter : WebApiParameter
{
    private readonly TableValuedParameter? TableParameter;

    public SqlType DbType { get; }
    public bool IsNullable { get; }

    public DBObjectName? UserDefinedType { get; }

    /// <summary>
    /// Do not add a parameter to db command if not provided by user. use db default
    /// </summary>
    public override bool RequiresUserProvidedValue => true;

    public DbApiParameter(string sqlName, string webApiName,
            SqlType sqlType, bool isNullable,
            DBObjectName? userDefinedType,
            IReadOnlyCollection<SqlFieldDescription>? userDefinedTypeFields,
            SwaggerGeneration.CodeConvention convention,
            NamingMappingHandler namingMappingHandler) : base(sqlName, webApiName)
    {
        this.DbType = sqlType;
        this.IsNullable = isNullable;
        UserDefinedType = userDefinedType;
        if (userDefinedType != null)
        {
            this.TableParameter = new TableValuedParameter(
                convention.GetUserDefinedSqlTypeWebApiName(userDefinedType),
                userDefinedType,
                namingMappingHandler, userDefinedTypeFields ?? throw new ArgumentNullException("userDefinedTypeFields"));
        }
    }





    public override IEnumerable<WebApiParameter> GetRequiredTypes()
    {
        if (TableParameter == null)
        {
            return Array.Empty<WebApiParameter>();
        }
        else
        {
            return new WebApiParameter[] { TableParameter };
        }
    }

    public override OpenApiSchema GetSchema()
    {
        OpenApiSchema property = new OpenApiSchema();
        property.Type = this.DbType.JsType;
        if (this.DbType.JsFormat != null)
        {
            property.Format = this.DbType.JsFormat;
        }
        property.Nullable = this.IsNullable;

        if (this.UserDefinedType != null)
        {
            property.UniqueItems = false;
            property.Items = new OpenApiSchema()
            {
                Reference = new OpenApiReference()
                {
                    Type = ReferenceType.Schema,
                    Id = TableParameter!.WebApiName
                }
            };
        }
        return property;
    }


    private XElement ToXml(IEnumerable<KeyValuePair<string, object>> input)
    {
        return new XElement("el",
            input.Select(k => new XAttribute(k.Key, k.Value)));
    }
    private XElement ToXml(IReadOnlyDictionary<string, object> input)
    {
        return new XElement("el",
            input.Keys.Select(k => new XAttribute(k, input[k])));
    }


    private XElement ToXml(IEnumerable<IReadOnlyDictionary<string, object>> input)
    {
        return new XElement("root",
            input.Select(s => ToXml(s)));
    }

    protected bool IsXmlParameter() => this.SqlName!.EndsWith("Xml") || this.DbType.DbType == "xml";


    public override object? GetValue(HttpContext? http, object? valueProvided, ApiParameterContext? apiParameterContext)
    {
        if (IsXmlParameter())
        {
            if (valueProvided is IEnumerable<KeyValuePair<string, object>> xobj1)
            {
                return ToXml(xobj1)?.ToString();
            }
            else if (valueProvided is IEnumerable<Dictionary<string, object>> xobj2)
            {
                return ToXml(xobj2)?.ToString();
            }
            else if (valueProvided is IEnumerable<IReadOnlyDictionary<string, object>> xobj3)
            {
                return ToXml(xobj3)?.ToString();
            }
            else if (valueProvided is IEnumerable<object> xobj4)
            {
                return ToXml(xobj4.Cast<IReadOnlyDictionary<string, object>>())?.ToString();
            }
        }
        if (this.DbType.NetType == typeof(System.Byte[]) && valueProvided is string str)
        {
            return Convert.FromBase64String(str);
        }
        if (this.UserDefinedType == null &&
            valueProvided != null &&
            this.DbType.NetType == typeof(System.String)
            && valueProvided is not string
            && (valueProvided is System.Collections.IEnumerable || valueProvided is IReadOnlyDictionary<string, object>))
        {
            return JsonHelper.SerializeObject(valueProvided);
        }
#if NEWTONSOFTJSON
        else if (valueProvided is Newtonsoft.Json.Linq.JArray ar)
        {
            if (this.UserDefinedType != null)
            {
                var jobjAr = ar.Cast<Newtonsoft.Json.Linq.JObject>()
                    .Select(oo => oo.Properties()
                        .ToDictionary(p => p.Name, p => p.Value.ToObject<object>())
                        ).ToArray();
                return TableParameter!.GetValue(http, jobjAr, apiParameterContext);
            }
            else
            {
                return JsonConvert.SerializeObject(valueProvided);
            }
        }
        else if (valueProvided is Newtonsoft.Json.Linq.JObject obj2)
        {
            if (this.UserDefinedType != null)
            {
                var jar_ob = new IDictionary<string, object?>[]
                {
                            obj2.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToObject<object?>())

                };
                return TableParameter!.GetValue(http, jar_ob, apiParameterContext);
            }
            else
            {
                return JsonConvert.SerializeObject(valueProvided);
            }
        }
#endif
        else
        {
            if (this.UserDefinedType != null)
            {
                return TableParameter!.GetValue(http, valueProvided, apiParameterContext);
            }
            return valueProvided;
        }
    }

}

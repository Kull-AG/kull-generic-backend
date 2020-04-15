using Kull.Data;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Kull.GenericBackend.SwaggerGeneration
{
    public class DbApiParameter : WebApiParameter
    {
        private readonly TableValuedParameter? TableParameter;

        public SqlType DbType { get; }
        public bool IsNullable { get; }

        public DBObjectName? UserDefinedType { get; }

        public DbApiParameter(string sqlName, string webApiName,
                SqlType sqlType, bool isNullable,
                DBObjectName userDefinedType,
                SqlHelper sqlHelper,
                NamingMappingHandler namingMappingHandler) : base(sqlName, webApiName)
        {
            this.DbType = sqlType;
            this.IsNullable = isNullable;
            UserDefinedType = userDefinedType;
            if (userDefinedType != null)
            {
                this.TableParameter = new TableValuedParameter(
                    GetSqlTypeWebApiName(this.UserDefinedType),
                    this.UserDefinedType,
                    sqlHelper, namingMappingHandler);
            }
        }



        private static string GetSqlTypeWebApiName(DBObjectName userDefinedType)
        {
            return (userDefinedType.Schema == "dbo" ? "" :
                                            userDefinedType.Schema + ".") + userDefinedType.Name;
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


        private XElement ToXml(IDictionary<string, object> input)
        {
            return new XElement("el",
                input.Keys.Select(k => new XAttribute(k, input[k])));
        }


        private XElement ToXml(IEnumerable<IDictionary<string, object>> input)
        {
            return new XElement("root",
                input.Select(s => ToXml(s)));
        }

        private byte[] GetByteFromStream(System.IO.Stream stream)
        {
            // Thanks,  https://stackoverflow.com/questions/1080442/how-to-convert-an-stream-into-a-byte-in-c
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }

        }

        public override object? GetValue(HttpContext http, object? valueProvided)
        {
            if (valueProvided is IDictionary<string, object> obj)
            {
                if (this.SqlName!.EndsWith("Xml") || this.DbType.DbType == "xml")
                {
                    return ToXml(obj)?.ToString();
                }
                else if (this.UserDefinedType != null)
                {
                    return this.TableParameter!.GetValue(http, new IDictionary<string, object>[] { obj });
                }
                else
                {
                    return JsonConvert.SerializeObject(obj);
                }
            }
            else if (valueProvided is IEnumerable<Dictionary<string, object>> objAr)
            {
                if (this.SqlName!.EndsWith("Xml") || this.DbType.DbType == "xml")
                {
                    return ToXml(objAr)?.ToString();
                }
                else if (this.UserDefinedType != null)
                {
                    return TableParameter!.GetValue(http, objAr);
                }
                else
                {
                    return JsonConvert.SerializeObject(objAr);
                }
            }
            else if (valueProvided is Newtonsoft.Json.Linq.JArray ar)
            {
                if (this.UserDefinedType != null)
                {
                    var jobjAr = ar.Cast<Newtonsoft.Json.Linq.JObject>()
                        .Select(oo => oo.Properties()
                            .ToDictionary(p => p.Name, p => p.Value.ToObject<object>())
                            ).ToArray();
                    return TableParameter!.GetValue(http, jobjAr);
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
                    var jar_ob = new IDictionary<string, object>[]
                    {
                            obj2.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToObject<object>())

                    };
                    return TableParameter!.GetValue(http, jar_ob);
                }
                else
                {
                    return JsonConvert.SerializeObject(valueProvided);
                }
            }
            else if (this.DbType.NetType == typeof(System.Byte[]) && valueProvided is string str)
            {
                return Convert.FromBase64String(str);
            }
            else if (valueProvided is System.IO.Stream stream)
            {
                var value = GetByteFromStream(stream);
                return value;
            }
            else if (valueProvided is Func<System.IO.Stream> streamAccessor)
            {
                using var streamReal = streamAccessor();
                var value = GetByteFromStream(streamReal);
                return value;
            }
            else
            {
                return valueProvided;
            }
        }
    }

}

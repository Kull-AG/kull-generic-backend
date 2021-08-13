using Kull.Data;
using Kull.GenericBackend.Common;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.SwaggerGeneration
{
    public class CodeConvention
    {
        public virtual string FirstResultKey {get;} = "value";
        public virtual string OtherResultsKey {get;} = "additionalValues";
        public virtual string OutputParametersKey {get;} = "out";

        /// <summary>
        /// A string to be used for Representation in URLs or Methods
        /// Eg for /Cases/{CaseId|int}/Brand returns GetCasesBy
        /// </summary>
        /// <returns></returns>
        public virtual string GetTag(Entity entity, Method method)
        {
            List<string?> result = new List<string?>();
            bool lastWasBy = false;

            foreach ((bool isParameterPart, string name, string? type) in entity.GetUrlParts())
            {
                if (!isParameterPart)
                {
                    lastWasBy = false;
                    result.Add(name);
                }
                else
                {
                    if (!lastWasBy)
                    {
                        lastWasBy = true;
                        string? lastUrlPart = result.LastOrDefault();

                        string? entnameprm = name.EndsWith("Id") ? name.Substring(0, name.Length - "Id".Length) : null;
                        string? entnamepart = lastUrlPart != null && lastUrlPart.EndsWith("s") ?
                            lastUrlPart.Substring(0, lastUrlPart.Length - "s".Length) : null;
                        if (entnameprm != null &&
                            entnameprm.Equals(entnamepart, StringComparison.CurrentCultureIgnoreCase))
                        {
                            result.RemoveAt(result.Count - 1);
                            result.Add(entnamepart);
                            continue;
                        }
                        else
                        {
                            result.Add("By");
                        }
                    }
                    var pascalCase = name[0].ToString().ToUpper() + name.Substring(1);
                    result.Add(pascalCase);
                }
            }
            return string.Join("", result);
        }

        /// <summary>
        /// Gets a unique operation name withing the tag
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="method"></param>
        /// <param name="operationType"></param>
        /// <returns></returns>
        public virtual string GetOperationName(Entity ent, Method method)
        {
            var operationType = method.HttpMethod;
            return operationType == OperationType.Post &&
                  (method.SP.Name.StartsWith("spAddUpdate") ||
                   method.SP.Name.StartsWith("sp_AddUpdate") ||
                   method.SP.Name.EndsWith("_AddUpdate")
                  ) ? "AddUpdate" :
              operationType == OperationType.Post ? "Add" :
              operationType == OperationType.Put ? "Update" :
              operationType == OperationType.Delete ? "Delete" :
              operationType == OperationType.Get ? "Get" :
              ToCamelCase(operationType.ToString());
        }

        /// <summary>
        /// Gets a unique OperationId among all tags, meaning in the whole api
        /// Results in x-operation-name in OpenApi
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="method"></param>
        /// <param name="operationType"></param>
        /// <returns></returns>
        public virtual string GetOperationId(Entity ent, Method method)
        {
           return GetOperationName(ent, method) + GetTag(ent, method);
        }

        private static string ToCamelCase(string key) => key[0].ToString().ToUpper() + key.Substring(1).ToLower();

        public virtual string GetParameterObjectName(Entity ent, Method method) =>
                 GetOperationId(ent, method) + "Parameters";

        protected string CleanName(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '-' || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
            //[a-zA-Z0-9\.\-_]+
        }

        public virtual string GetResultTypeName(Method method) => CleanName(method.SP.Name) + "Result";
        public virtual string GetOutputObjectTypeName(Method method) => CleanName(method.SP.Name) + "Output";


        public virtual string GetUserDefinedSqlTypeWebApiName(DBObjectName userDefinedType)
        {
            return (userDefinedType.Schema == "dbo" || userDefinedType.Schema == null ? "" :
                                            userDefinedType.Schema + "_") + userDefinedType.Name;
        }
    }
}

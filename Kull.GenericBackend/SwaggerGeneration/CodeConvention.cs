using Kull.Data;
using Kull.GenericBackend.Common;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.SwaggerGeneration
{
    public class CodeConvention
    {

        public virtual string GetOperationId(Entity ent, Method method, OperationType operationType)
        {
            return (
           operationType == OperationType.Post &&
               (method.SP.Name.StartsWith("spAddUpdate") ||
                method.SP.Name.StartsWith("sp_AddUpdate") ||
                method.SP.Name.EndsWith("_AddUpdate")
               ) ? "AddUpdate" :
           operationType == OperationType.Post ? "Add" :
           operationType == OperationType.Put ? "Update" :
           operationType == OperationType.Delete ? "Delete" :
           operationType == OperationType.Get ? "Get" :
           method.HttpMethod)
           + ent.GetDisplayString();
        }

        private static string ToCamelCase(string key) => key[0].ToString().ToUpper() + key.Substring(1).ToLower();

        public virtual string GetParameterObjectName(Entity ent, Method method, string HttpMethod) =>
                 ent.GetDisplayString() + ToCamelCase(HttpMethod) + "Parameters";

        public virtual string GetResultTypeName(DBObjectName name) => name.Name + "Result";
    }
}

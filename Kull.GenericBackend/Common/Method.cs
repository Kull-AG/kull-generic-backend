using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kull.GenericBackend.Common
{
    /// <summary>
    /// Represents an HTTP Method that is mapped to a Stored Procedure
    /// </summary>
    public class Method
    {
        /// <summary>
        /// The Http Method
        /// </summary>
        public OperationType HttpMethod { get; }


        public Data.DBObjectName SP { get; }

        public string? OperationId { get; }
        
        public string? OperationName { get; }

        public string? ResultType { get; }
        public string? Tag { get; }


        public Method(OperationType httpMethod, string sp)
            : this(httpMethod, sp, null, null, null)
        {

        }

        private Method(OperationType httpMethod, string sp, 
            string? operationId = null,
            string? operationName = null,
            string? resultType = null, string? tag = null)
        {
            HttpMethod = httpMethod;
            SP = sp;
            OperationId = operationId;
            OperationName = operationName;
            ResultType = resultType;
            Tag = tag;
        }

        internal static Method GetFromSection(IConfigurationSection section)
        {
            if (!Enum.TryParse(section.Key, true, out OperationType operationType))
            {
                throw new ArgumentException("Key must be a Http Method");
            }
            if (section.Value != null)
                return new Method(operationType, section.Value);
            
            return new Method(operationType, section.GetSection("SP").Value,
                section.GetSection("OperationId")?.Value,
                section.GetSection("OperationName")?.Value,
                section.GetSection("ResultType")?.Value,
                section.GetSection("Tag")?.Value);

        }

        /// <summary>
        /// Return the Http Method as string. Mainly for debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return HttpMethod + " (" + SP + ")";
        }

    }
}

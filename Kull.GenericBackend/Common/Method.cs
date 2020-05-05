using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
        public string HttpMethod { get; }
        public Data.DBObjectName SP { get; }

        public string? OperationId { get; }

        public string? ResultType { get; }


        public Method(string httpMethod, string sp, string? operationId, string? resultType)
        {
            HttpMethod = httpMethod.ToUpper();
            SP = sp;
            OperationId = operationId;
            ResultType = resultType;
        }
        internal static Method GetFromSection(IConfigurationSection section)
        {
            if (section.Value != null)
                return new Method(section.Key, section.Value, null, null);
            return new Method(section.Key, section.GetSection("SP").Value, section.GetSection("OperationId")?.Value, section.GetSection("ResultType")?.Value);

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

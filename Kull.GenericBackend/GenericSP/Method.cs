using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kull.GenericBackend.GenericSP
{
    /// <summary>
    /// Represents an HTTP Method that is mapped to a Stored Procedure
    /// </summary>
    public class Method
    {
        public string HttpMethod { get; }
        public Kull.Data.DBObjectName SP { get; }

        public string OperationId { get; }

        public Method(string httpMethod, string sp, string operationId)
        {
            this.HttpMethod = httpMethod;
            this.SP = sp;
            this.OperationId = operationId;
        }
        public static Method GetFromSection(IConfigurationSection section)
        {
            if (section.Value != null)
                return new Method(section.Key, section.Value, null);
            return new Method(section.Key, section.GetSection("SP").Value, section.GetSection("OperationId")?.Value);
            
        }

    }
}

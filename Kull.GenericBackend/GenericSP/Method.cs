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
        public string Name { get; }
        public Kull.Data.DBObjectName SP { get; }

        public Method(string name, string sp)
        {
            this.Name = name;
            this.SP = sp;
        }
        public static Method GetFromSection(IConfigurationSection section)
        {
            return new Method(section.Key, section.Value);
        }

        public Model.SPParameter[] GetParameters(DbConnection con, Model.SPParametersProvider sPParametersProvider)
        {
            return sPParametersProvider.GetSPParameters(this.SP, con);
        }
    }
}

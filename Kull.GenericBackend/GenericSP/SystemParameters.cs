using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.GenericSP
{
    public class SystemParameters
    {
        private Dictionary<string, Func<HttpContext, object>> getFns = new Dictionary<string, Func<HttpContext, object>>(
            StringComparer.CurrentCultureIgnoreCase)
        {
            { "NTLogin", s=> GetUserName(s) },
            { "ADLogin", s=> GetUserName(s) },
            { "IPAddress", c=>c.Connection.RemoteIpAddress.ToString() },
            { "UserAgent", c=>c.Request.Headers["User-Agent"].ToString() }
        };



        private static string GetUserName(HttpContext context)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return "KULL\\Ehrsam";
            }
#endif
            return context.User.Identity.Name;
        }

        public string[] GetSystemParameters()
        {
            return getFns.Keys.ToArray();
        }

        public object GetValue(string key, HttpContext context)
        {
            return getFns[key](context);
        }

        internal bool IsSystemParameter(string parameterName)
        {
            return getFns.ContainsKey(parameterName);
        }

        public void AddSystemParameter(string name, Func<HttpContext, object> valueAccessor)
        {
            this.getFns.Add(name, valueAccessor);
        }
    }
}

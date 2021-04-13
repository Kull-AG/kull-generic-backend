#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.Filter
{
    public class SystemParameters: IParameterInterceptor
    {
        /// <summary>
        /// Set this if you want to use always the same user while debugging
        /// </summary>
        public static string DebugUsername = "KULL\\Ehrsam";

        private Dictionary<string, Func<HttpContext, object?>> getFns = new Dictionary<string, Func<HttpContext, object?>>(
            StringComparer.CurrentCultureIgnoreCase)
        {
            { "NTLogin", s=> GetUserName(s) },
            { "ADLogin", s=> GetUserName(s) },
#if NETFX
            { "IPAddress", c=>c.Request.ServerVariables["REMOTE_ADDR"] ?? "No ip"},
#else
            { "IPAddress", c=>c.Connection?.RemoteIpAddress?.ToString() ?? "No ip"},
#endif
            { "UserAgent", c=>c.Request.Headers["User-Agent"].ToString() }
        };



        private static string? GetUserName(HttpContext context)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return DebugUsername;
            }
#endif
            return context.User?.Identity?.Name;
        }

        public string[] GetSystemParameters()
        {
            return getFns.Keys.ToArray();
        }

        protected object? GetValue(string key, HttpContext context)
        {
            return getFns[key](context);
        }

        protected bool IsSystemParameter(string parameterName)
        {
            return getFns.ContainsKey(parameterName);
        }

        /// <summary>
        /// Add a custom system paramters
        /// </summary>
        /// <param name="name"></param>
        /// <param name="valueAccessor"></param>
        public void AddSystemParameter(string name, Func<HttpContext, object> valueAccessor)
        {
            this.getFns.Add(name, valueAccessor);
        }

        /// <summary>
        /// Remove all current system parameters
        /// </summary>
        public void Clear()
        {
            this.getFns.Clear();
        }

        void IParameterInterceptor.Intercept(ICollection<Parameters.WebApiParameter> apiParams, ParameterInterceptorContext parameterInterceptorContext)
        {
            List<Parameters.WebApiParameter> toRemove = new List<Parameters.WebApiParameter>();
            List<Parameters.WebApiParameter> toAdd = new List<Parameters.WebApiParameter>();
            foreach(var param in apiParams)
            {
                if(param.SqlName != null && IsSystemParameter(param.SqlName))
                {
                    toRemove.Add(param);
                    toAdd.Add(new Parameters.SystemParameter(param.SqlName,
                            getFns[param.SqlName]));
                }
            }
            foreach (var i in toRemove) apiParams.Remove(i);
            foreach (var a in toAdd) apiParams.Add(a);
        }

        
    }
}

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Kull.GenericBackend.Filter
{
    /// <summary>
    /// An intercepter that is executed just before executing the database request
    /// Use this if you want to do some custom auth check or the like
    /// </summary>
    public interface IRequestInterceptor
    {
        /// <summary>
        /// This will be executed before the database is reached. 
        /// </summary>
        /// <param name="httpContext">The http context</param>
        /// <param name="context">Further information about what request is going on</param>
        /// <returns>a status code and the content to be written to the client. If you return null, the Request will continue</returns>
        public (int statusCode, HttpContent responseContent)? OnBeforeRequest(HttpContext httpContext, RequestInterceptorContext context);
    }
}

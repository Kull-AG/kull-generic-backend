using Kull.GenericBackend.Filter;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kull.GenericBackend.IntegrationTest
{
    public class TestRequestInterceptor : Filter.IRequestInterceptor
    {
        public (int statusCode, HttpContent responseContent)? OnBeforeRequest(HttpContext httpContext, RequestInterceptorContext context)
        {
            if (context.Method.DbObject.Name.ToString() == "None")
            {
                var cont = new StringContent("Hey, I am a teapot");
                cont.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                cont.Headers.Add("X-Test", "Test string");
                return (418, cont);
            }
            return null;
        }
    }
}

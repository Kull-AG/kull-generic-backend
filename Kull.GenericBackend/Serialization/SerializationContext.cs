using Kull.GenericBackend.Common;
#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
using System.Collections;
#endif
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Provides information for the IGenericSPSerializer's
    /// </summary>
    public class SerializationContext
    {
        protected readonly DbCommand cmd;

        private HttpContext httpContext;

#if NETFX
        public System.IO.Stream OutputStream => httpContext.Response.OutputStream;
#else
        public System.IO.Stream OutputStream => httpContext.Response.Body;
#endif

#if NETFX
        public bool HasResponseStarted => httpContext.Response.HeadersWritten;
#else
        public bool HasResponseStarted => httpContext.Response.HasStarted;
#endif

        public async Task FlushResponseAsync()
        {
            await OutputStream.FlushAsync();
#if NET48
            await httpContext.Response.FlushAsync();
#endif

        }

        public void SetHeaders(string contentType, int statusCode, bool noCache, IDictionary<string, string?>? headers=null)
        {

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = contentType;
            if (headers != null)
            {
                foreach (var h in headers)
                {
                    httpContext.Response.Headers[h.Key] = h.Value;
                }
            }
            if (noCache)
            {
                httpContext.Response.Headers["Cache-Control"] = "no-store";
                httpContext.Response.Headers["Expires"] = "0";
            }
        }

        public Method Method { get; }
        public Entity Entity { get; }

        /// <summary>
        /// Use this if you want to override this class
        /// </summary>
        /// <param name="baseSerializationContext"></param>
        protected SerializationContext(SerializationContext baseSerializationContext)
            :this(baseSerializationContext.cmd, baseSerializationContext.httpContext,
                    baseSerializationContext.Method, baseSerializationContext.Entity)
        {

        }

        internal SerializationContext(DbCommand cmd, HttpContext httpContext, Method method, Entity entity)
        {
            this.cmd = cmd;
            this.httpContext = httpContext;
            Method = method;
            Entity = entity;
        }

        public string? GetRequestHeader(string headerName)
        {
            return httpContext.Request.Headers[headerName];
        }

        public IOrderedEnumerable<System.Net.Http.Headers.MediaTypeWithQualityHeaderValue>? GetAcceptHeader() =>
            GetRequestHeader("Accept")?.Split(',')
        .Select(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse)
        .OrderByDescending(mt => mt.Quality.GetValueOrDefault(1));

#if NET48
        public virtual Task<DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior commandBehavior=System.Data.CommandBehavior.Default) => cmd.ExecuteReaderAsync(commandBehavior);
        public virtual Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync();

#else
        public virtual Task<DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior commandBehavior=System.Data.CommandBehavior.Default) => cmd.ExecuteReaderAsync(commandBehavior, httpContext.RequestAborted);
        public virtual Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync(httpContext.RequestAborted);

#endif
        public virtual IEnumerable<DbParameter> GetParameters() => cmd.Parameters.Cast<DbParameter>();
        public override string ToString()
        {
            return Method.HttpMethod.ToString() + " " + Entity.ToString() + ": " + Method.SP;
        }


        public Task HttpContentToResponse(System.Net.Http.HttpContent content)
        {
            var response = this.httpContext.Response;
            return HttpHandlingUtils.HttpContentToResponse(content, response);
        }

        public void RequireSyncIO()
        {
#if !NETSTD2 && !NETFX
            var syncIOFeature = httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }
#endif
        }
    }
}

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
    public abstract class SerializationContext
    {

        public virtual int? ForceStatusCode { get; } = null;

        protected readonly HttpContext httpContext;

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

            httpContext.Response.StatusCode = this.ForceStatusCode ?? statusCode;
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

        internal SerializationContext(HttpContext httpContext, Method method, Entity entity)
        {
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


        public abstract Task<DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior commandBehavior=System.Data.CommandBehavior.Default);
        public abstract Task<int> ExecuteNonQueryAsync();

        public abstract IEnumerable<DbParameter> GetParameters();
        public override string ToString()
        {
            return Method.HttpMethod.ToString() + " " + Entity.ToString() + ": " + Method.DbObject;
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

    internal class SerializationContextCmd: SerializationContext
    {

        protected readonly DbCommand cmd;

        public SerializationContextCmd(DbCommand cmd, HttpContext httpContext, Method method, Entity entity) : base(httpContext, method, entity)
        {
            this.cmd = cmd;
        }

#if NET48
        public override Task<DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior commandBehavior=System.Data.CommandBehavior.Default) => cmd.ExecuteReaderAsync(commandBehavior);
        public override Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync();

#else
        public override Task<DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior commandBehavior = System.Data.CommandBehavior.Default) => cmd.ExecuteReaderAsync(commandBehavior, httpContext.RequestAborted);
        public override Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync(httpContext.RequestAborted);
#endif

        public override IEnumerable<DbParameter> GetParameters() => cmd.Parameters.Cast<DbParameter>();

    }

    internal class SerializationContextResult : SerializationContext
    {

        protected readonly DbDataReader reader;

        readonly int? forceStatusCode;
        public override int? ForceStatusCode => forceStatusCode;

        public SerializationContextResult(DbDataReader reader, HttpContext httpContext, Method method, Entity entity, int? forceStatusCode) : base(httpContext, method, entity)
        {
            this.reader = reader;
            this.forceStatusCode = forceStatusCode;
        }

        public override Task<DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior commandBehavior = System.Data.CommandBehavior.Default) => Task.FromResult(this.reader);
        public override Task<int> ExecuteNonQueryAsync() => Task.FromResult(0);


        public override IEnumerable<DbParameter> GetParameters() => Array.Empty<DbParameter>();

    }
}

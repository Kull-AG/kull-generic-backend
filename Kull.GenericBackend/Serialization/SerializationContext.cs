using Kull.GenericBackend.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Serialization
{
    public class SerializationContext
    {
        private readonly DbCommand cmd;
        public HttpContext HttpContext { get; }
        public Method Method { get; }
        public Entity Entity { get; }

        internal SerializationContext(DbCommand cmd, HttpContext httpContext, Method method, Entity entity)
        {
            this.cmd = cmd;
            HttpContext = httpContext;
            Method = method;
            Entity = entity;
        }

        public Task<DbDataReader> ExecuteReaderAsync() => cmd.ExecuteReaderAsync(HttpContext.RequestAborted);

        public override string ToString()
        {
            return Method.HttpMethod.ToString() + " " + Entity.ToString() + ": " + Method.SP;
        }
    }
}

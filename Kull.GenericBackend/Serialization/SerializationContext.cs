using Kull.GenericBackend.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Provides information for the IGenericSPSerializer's
    /// </summary>
    public class SerializationContext
    {
        protected readonly DbCommand cmd;
        public HttpContext HttpContext { get; }
        public Method Method { get; }
        public Entity Entity { get; }

        /// <summary>
        /// Use this if you want to override this class
        /// </summary>
        /// <param name="baseSerializationContext"></param>
        protected SerializationContext(SerializationContext baseSerializationContext)
            :this(baseSerializationContext.cmd, baseSerializationContext.HttpContext,
                    baseSerializationContext.Method, baseSerializationContext.Entity)
        {

        }

        internal SerializationContext(DbCommand cmd, HttpContext httpContext, Method method, Entity entity)
        {
            this.cmd = cmd;
            HttpContext = httpContext;
            Method = method;
            Entity = entity;
        }

        public virtual Task<DbDataReader> ExecuteReaderAsync() => cmd.ExecuteReaderAsync(HttpContext.RequestAborted);
        public virtual Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync(HttpContext.RequestAborted);

        public override string ToString()
        {
            return Method.HttpMethod.ToString() + " " + Entity.ToString() + ": " + Method.SP;
        }
    }
}

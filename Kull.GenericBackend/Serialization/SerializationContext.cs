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

#if NET48
        public virtual Task<DbDataReader> ExecuteReaderAsync() => cmd.ExecuteReaderAsync();
        public virtual Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync();

#else
        public virtual Task<DbDataReader> ExecuteReaderAsync() => cmd.ExecuteReaderAsync(HttpContext.RequestAborted);
        public virtual Task<int> ExecuteNonQueryAsync() => cmd.ExecuteNonQueryAsync(HttpContext.RequestAborted);

#endif
        public virtual IEnumerable<DbParameter> GetParameters() => cmd.Parameters.Cast<DbParameter>();
        public override string ToString()
        {
            return Method.HttpMethod.ToString() + " " + Entity.ToString() + ": " + Method.SP;
        }
    }
}

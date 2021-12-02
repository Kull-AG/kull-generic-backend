using Kull.GenericBackend.Common;
using System.Data.Common;

namespace Kull.GenericBackend.Filter;

/// <summary>
/// Provides information for a Request Interceptor
/// </summary>
public class RequestInterceptorContext
{
    /// <summary>
    /// The entity, representing a url
    /// </summary>
    public Entity Entity { get; }

    /// <summary>
    /// The HTTP Method
    /// </summary>
    public Method Method { get; }

    /// <summary>
    /// The database connection that will be used for the Request/Command
    /// </summary>
    public DbConnection DbConnection { get; }

    internal RequestInterceptorContext(Entity ent, Method method, DbConnection dbConnection)
    {
        this.Entity = ent;
        this.Method = method;
        DbConnection = dbConnection;
    }
}

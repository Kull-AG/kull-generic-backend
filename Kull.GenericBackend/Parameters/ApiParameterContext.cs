using Kull.GenericBackend.Common;

namespace Kull.GenericBackend.Parameters;

public class ApiParameterContext
{
    public Entity Entity { get; }
    public Method Method { get; }
    internal ApiParameterContext(Entity entity, Method method)
    {
        this.Entity = entity;
        this.Method = method;
    }
}

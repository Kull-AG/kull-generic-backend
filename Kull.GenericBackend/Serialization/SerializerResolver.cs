using Kull.GenericBackend.Common;
using System.Collections.Generic;
#if NET48
using System.Net.Http.Headers;
#else
using Microsoft.Net.Http.Headers;
#endif

namespace Kull.GenericBackend.Serialization;

public sealed class SerializerResolver
{
    private readonly IEnumerable<IGenericSPSerializer> serializers;
    private readonly IList<MediaTypeHeaderValue> defaultAccept = new List<MediaTypeHeaderValue>() {
                     new MediaTypeHeaderValue("application/json")
                     };

    public SerializerResolver(
            IEnumerable<IGenericSPSerializer> serializers)
    {
        this.serializers = serializers;
    }

    public IGenericSPSerializer? GetSerialializerOrNull(IList<MediaTypeHeaderValue>? acceptHeaders,
        Entity entity,
        Method method)
    {
        IGenericSPSerializer? serializer = null;
        var accept = acceptHeaders ?? defaultAccept;
        if (accept.Count == 0)
        {
            // .Net Core 3 seems to use length 0 instead of null
            accept = defaultAccept;
        }
        int lastPrio = int.MaxValue;
        foreach (var ser in serializers)
        {
            if (method.ResultType != null && !ser.SupportsResultType(method.ResultType.ToLower()))
                continue;
            int? prio = ser.GetSerializerPriority(accept, entity, method);
            if (prio == null && method.ResultType != null)
            {
                prio = 1000000;
            }
            if (prio != null && prio < lastPrio)
            {
                serializer = ser;
                lastPrio = prio.Value;
                if (lastPrio == 0) // Cannot get any more priority
                    break;
            }
        }
        return serializer;
    }
}

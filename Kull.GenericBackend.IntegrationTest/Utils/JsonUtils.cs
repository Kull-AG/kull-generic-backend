using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Kull.GenericBackend.IntegrationTest.Utils;

public static class JsonUtils
{
    public static void AssertJsonEquals(object expected, object actual)
    {
        var obj1 = GetJToken(expected);
        var obj2 = GetJToken(actual);

        if (JToken.DeepEquals(obj1, obj2))
        {
            Assert.True(true, "Json equals");
        }
        else
        {
            Assert.Equal(obj1.ToString(), obj2.ToString());
        }
    }

    public static bool JsonEquals(object json1, object json2)
    {
        var obj1 = GetJToken(json1);
        var obj2 = GetJToken(json2);

        return JToken.DeepEquals(obj1, obj2);
    }

    private static JToken GetJToken(object json1)
    {
        if (json1 is JToken j) return j;
        if (json1 is string json) return JsonConvert.DeserializeObject<JToken>(json);
        return JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(json1));
    }
}

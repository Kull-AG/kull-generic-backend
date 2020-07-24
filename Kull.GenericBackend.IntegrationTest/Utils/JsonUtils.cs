using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Kull.GenericBackend.IntegrationTest.Utils
{
    public class JsonUtils
    {
        public bool JsonEquals(string json1, string json2)
        {
            var obj1 = JsonConvert.DeserializeObject<JToken>(json1);
            var obj2 = JsonConvert.DeserializeObject<JToken>(json2);
            return JToken.DeepEquals(obj1, obj2);

        }
    }

}
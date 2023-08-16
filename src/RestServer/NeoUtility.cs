using Neo.VM.Types;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Numerics;
using Array = Neo.VM.Types.Array;
using Boolean = Neo.VM.Types.Boolean;
using Buffer = Neo.VM.Types.Buffer;

namespace Neo.Plugins.RestServer
{
    public static class RestServerUtility
    {

        public static StackItem StackItemFromJToken(JToken json)
        {
            StackItem s = StackItem.Null;
            var type = (StackItemType)Enum.Parse(typeof(StackItemType), json["type"].Value<string>(), true);
            var value = json["value"];

            switch (type)
            {
                case StackItemType.Struct:
                    var st = new Struct();
                    foreach (var item in (JArray)value)
                        st.Add(StackItemFromJToken(item));
                    s = st;
                    break;
                case StackItemType.Array:
                    var a = new Array();
                    foreach (var item in (JArray)value)
                        a.Add(StackItemFromJToken(item));
                    s = a;
                    break;
                case StackItemType.Map:
                    var m = new Map();
                    foreach (var item in (JArray)value)
                    {
                        var key = (PrimitiveType)StackItemFromJToken(item["Key"]);
                        m[key] = StackItemFromJToken(item["Value"]);
                    }
                    s = m;
                    break;
                case StackItemType.Boolean:
                    s = value.ToObject<bool>() ? StackItem.True : StackItem.False;
                    break;
                case StackItemType.Buffer:
                    s = new Buffer(Convert.FromBase64String(value.ToObject<string>()));
                    break;
                case StackItemType.ByteString:
                    s = new ByteString(Convert.FromBase64String(value.ToObject<string>()));
                    break;
                case StackItemType.Integer:
                    s = value.ToObject<BigInteger>();
                    break;
                case StackItemType.InteropInterface:
                    s = new InteropInterface(Convert.FromBase64String(value.ToObject<string>()));
                    break;
                case StackItemType.Pointer:
                    s = new Pointer(null, value.ToObject<int>());
                    break;
                default:
                    break;
            }
            return s;
        }

        public static JToken StackItemToJToken(StackItem item, IList<(StackItem, JToken)> context)
        {
            JToken o = null;
            switch (item)
            {
                case Struct @struct:
                    if (context is null)
                        context = new List<(StackItem, JToken)>();
                    else
                        (_, o) = context.FirstOrDefault(f => ReferenceEquals(f.Item1, item));
                    if (o is null)
                    {
                        context.Add((item, o));
                        var a = @struct.Select(s => StackItemToJToken(s, context));
                        o = new JObject()
                        {
                            new JProperty("type", StackItemType.Struct.ToString()),
                            new JProperty("value", JArray.FromObject(a)),
                        };
                    }
                    break;
                case Array array:
                    if (context is null)
                        context = new List<(StackItem, JToken)>();
                    else
                        (_, o) = context.FirstOrDefault(f => ReferenceEquals(f.Item1, item));
                    if (o is null)
                    {
                        context.Add((item, o));
                        var a = array.Select(s => StackItemToJToken(s, context));
                        o = new JObject()
                        {
                            new JProperty("type", StackItemType.Array.ToString()),
                            new JProperty("value", JArray.FromObject(a)),
                        };
                    }
                    break;
                case Map map:
                    if (context is null)
                        context = new List<(StackItem, JToken)>();
                    else
                        (_, o) = context.FirstOrDefault(f => ReferenceEquals(f.Item1, item));
                    if (o is null)
                    {
                        context.Add((item, o));
                        var kvp = map.Select(s => new KeyValuePair<JToken, JToken>(StackItemToJToken(s.Key, context), StackItemToJToken(s.Value, context)));
                        o = new JObject()
                        {
                            new JProperty("type", StackItemType.Map.ToString()),
                            new JProperty("value", JArray.FromObject(kvp)),
                        };
                    }
                    break;
                case Boolean:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Boolean.ToString()),
                        new JProperty("value", item.GetBoolean()),
                    };
                    break;
                case Buffer:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Buffer.ToString()),
                        new JProperty("value", Convert.ToBase64String(item.GetSpan())),
                    };
                    break;
                case ByteString:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.ByteString.ToString()),
                        new JProperty("value", Convert.ToBase64String(item.GetSpan())),
                    };
                    break;
                case Integer:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Integer.ToString()),
                        new JProperty("value", item.GetInteger()),
                    };
                    break;
                case InteropInterface:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.InteropInterface.ToString()),
                        new JProperty("value", JToken.Parse(
                            JsonConvert.SerializeObject(
                                item.GetInterface<object>(),
                                RestServerSettings.Default.JsonSerializerSettings
                            )
                        )),
                    };
                    break;
                case Pointer pointer:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Pointer.ToString()),
                        new JProperty("value", pointer.Position),
                    };
                    break;
                case Null:
                    o = new JObject()
                    {
                        new JProperty("type", StackItemType.Any.ToString()),
                        new JProperty("value", null),
                    };
                    break;
                default:
                    throw new NotImplementedException($"StackItemType({item.Type}) is not supported to JSON.");
            }
            return o;
        }
    }
}

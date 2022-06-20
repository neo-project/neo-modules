using System.Collections.Generic;
using System.Text;
using System.Web;
using Neo.IO;
using Neo.IO.Json;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.FileStorage.Listen
{
    public static class Helper
    {
        public static string ParseToString(this IDictionary<string, string> parameters)
        {
            if (parameters.Count == 0) return string.Empty;
            IEnumerator<KeyValuePair<string, string>> dem = parameters.GetEnumerator();
            StringBuilder query = new();
            while (dem.MoveNext())
            {
                string key = dem.Current.Key;
                string value = dem.Current.Value;
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    query.Append(HttpUtility.UrlEncode(key)).Append('=').Append(HttpUtility.UrlEncode(value)).Append('&');
                }
            }
            return query.ToString().Substring(0, query.Length - 1);
        }

        public static JObject ParseToJson(this NotifyEventArgs notify)
        {
            var container = notify.ScriptContainer.ToArray().ToHexString();
            var scriptHash = notify.ScriptHash.ToArray().ToHexString();
            var eventName = notify.EventName;
            var enumerator = notify.State.GetEnumerator();
            var state = new JArray();
            while (enumerator.MoveNext())
            {
                state.Add(enumerator.Current.ToJson());
            }
            return new JArray() { container, scriptHash, eventName, state };
        }
    }
}

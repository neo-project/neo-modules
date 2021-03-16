using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.FileStorage.Tests
{
    internal static class TestUtils
    {
        public readonly static List<RpcTestCase> RpcTestCases = ((JArray)JObject.Parse(File.ReadAllText("./Config/MainContractTestCases.json"))).Select(p => RpcTestCase.FromJson(p)).ToList();
    }

    internal class RpcTestCase
    {
        public string Name { get; set; }
        public RpcRequest Request { get; set; }
        public RpcResponse Response { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["Name"] = Name,
                ["Request"] = Request.ToJson(),
                ["Response"] = Response.ToJson(),
            };
        }

        public static RpcTestCase FromJson(JObject json)
        {
            return new RpcTestCase
            {
                Name = json["Name"].AsString(),
                Request = RpcRequest.FromJson(json["Request"]),
                Response = RpcResponse.FromJson(json["Response"]),
            };
        }

    }
}

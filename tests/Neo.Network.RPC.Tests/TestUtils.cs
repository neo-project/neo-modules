using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Network.RPC.Tests
{
    internal static class TestUtils
    {
        public readonly static List<RpcTestCase> RpcTestCases = ((JArray)JObject.Parse(File.ReadAllText("RpcTestCases.json"))).Select(p => RpcTestCase.FromJson(p)).ToList();

        public static Block GetBlock(int txCount)
        {
            return new Block
            {
                Header = new Header
                {
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero,
                    Witness = new Witness
                    {
                        InvocationScript = new byte[0],
                        VerificationScript = new byte[0]
                    }
                },
                Transactions = Enumerable.Range(0, txCount).Select(p => GetTransaction()).ToArray()
            };
        }

        public static Header GetHeader()
        {
            return GetBlock(0).Header;
        }

        public static Transaction GetTransaction()
        {
            return new Transaction
            {
                Script = new byte[1],
                Signers = new Signer[] { new Signer { Account = UInt160.Zero } },
                Attributes = new TransactionAttribute[0],
                Witnesses = new Witness[]
                {
                    new Witness
                    {
                        InvocationScript = new byte[0],
                        VerificationScript = new byte[0]
                    }
                }
            };
        }
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

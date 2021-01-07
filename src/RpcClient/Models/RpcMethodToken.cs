using Neo.IO.Json;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Models
{
    class RpcMethodToken
    {
        public static MethodToken FromJson(JObject json)
        {
            return new MethodToken
            {
                Hash = UInt160.Parse(json["hash"].AsString()),
                Method = json["method"].AsString(),
                ParametersCount = (ushort)json["paramcount"].AsNumber(),
                HasReturnValue = json["hasreturnvalue"].AsBoolean(),
                CallFlags = (CallFlags)Enum.Parse(typeof(CallFlags), json["callflags"].AsString())
            };
        }
    }
}

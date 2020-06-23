using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.IO;

namespace Neo.SmartContract.Native.Tokens
{
    public class OracleHttpRequest : OracleRequest
    {
        public HttpMethod Method = HttpMethod.GET;
    }
}

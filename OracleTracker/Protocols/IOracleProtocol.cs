using Neo.SmartContract.Native.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace OracleTracker.Protocols
{
    interface IOracleProtocol
    {
        OracleResponse Process(OracleRequest request);
    }
}

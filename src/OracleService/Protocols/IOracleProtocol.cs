using Neo.Network.P2P.Payloads;
using System;

namespace Neo.Plugins
{
    interface IOracleProtocol
    {
        OracleResponseCode Process(Uri uri, out string response);
    }
}

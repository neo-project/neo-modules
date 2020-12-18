using Neo.Network.P2P.Payloads;
using System;

namespace Neo.Plugins
{
    interface IOracleProtocol : IDisposable
    {
        void Configure();
        OracleResponseCode Process(Uri uri, out string response);
    }
}

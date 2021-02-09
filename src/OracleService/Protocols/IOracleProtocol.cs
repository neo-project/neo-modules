using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    interface IOracleProtocol : IDisposable
    {
        void Configure();
        Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation);
    }
}

using Neo.Cryptography.ECC;
using Neo.FileSystem.API.Client;
using Neo.FileSystem.API.Client.ObjectParams;
using Neo.FileSystem.API.Cryptography;
using Neo.FileSystem.API.Refs;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Object = Neo.FileSystem.API.Object.Object;
using Range = Neo.FileSystem.API.Object.Range;

namespace Neo.Plugins
{
    class OracleNeoFSProtocol : IOracleProtocol
    {
        private readonly System.Security.Cryptography.ECDsa privateKey;

        public OracleNeoFSProtocol(Wallet wallet, ECPoint[] oracles)
        {
            byte[] key = oracles.Select(p => wallet.GetAccount(p)).Where(p => p is not null && p.HasKey && !p.Lock).FirstOrDefault().GetKey().PrivateKey;
            privateKey = key.LoadPrivateKey();
        }

        public void Configure()
        {
        }

        public void Dispose()
        {
            privateKey.Dispose();
        }

        public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
        {
            Utility.Log(nameof(OracleNeoFSProtocol), LogLevel.Debug, $"Request: {uri.AbsoluteUri}");
            try
            {
                byte[] res = await GetAsync(uri, Settings.Default.NeoFS.EndPoint, cancellation);
                Utility.Log(nameof(OracleNeoFSProtocol), LogLevel.Debug, $"NeoFS result: {res.ToHexString()}");
                return (OracleResponseCode.Success, Convert.ToBase64String(res));
            }
            catch (Exception e)
            {
                Utility.Log(nameof(OracleNeoFSProtocol), LogLevel.Debug, $"NeoFS result: error,{e.Message}");
                return (OracleResponseCode.Error, null);
            }
        }

        private Task<byte[]> GetAsync(Uri uri, string host, CancellationToken cancellation)
        {
            string[] ps = uri.AbsolutePath.Split("/");
            if (ps.Length < 2) throw new FormatException("Invalid neofs url");
            ContainerID containerID = ContainerID.FromBase58String(ps[0]);
            ObjectID objectID = ObjectID.FromBase58String(ps[1]);
            Address objectAddr = new()
            {
                ContainerId = containerID,
                ObjectId = objectID
            };
            Client client = new(privateKey, host);
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            tokenSource.CancelAfter(Settings.Default.NeoFS.Timeout);
            if (ps.Length == 2)
                return GetPayloadAsync(client, objectAddr, tokenSource.Token);
            return ps[2] switch
            {
                "range" => GetRangeAsync(client, objectAddr, ps.Skip(3).ToArray(), tokenSource.Token),
                "header" => GetHeaderAsync(client, objectAddr, tokenSource.Token),
                "hash" => GetHashAsync(client, objectAddr, ps.Skip(3).ToArray(), tokenSource.Token),
                _ => throw new Exception("invalid command")
            };
        }

        private static async Task<byte[]> GetPayloadAsync(Client client, Address addr, CancellationToken cancellation)
        {
            Object obj = await client.GetObject(cancellation, new GetObjectParams() { Address = addr }, new CallOptions { Ttl = 2 });
            return obj.Payload.ToByteArray();
        }

        private static Task<byte[]> GetRangeAsync(Client client, Address addr, string[] ps, CancellationToken cancellation)
        {
            if (ps.Length == 0) throw new FormatException("missing object range (expected 'Offset|Length')");
            Range range = ParseRange(ps[0]);
            return client.GetObjectPayloadRangeData(cancellation, new RangeDataParams() { Address = addr, Range = range }, new CallOptions { Ttl = 2 });
        }

        private static async Task<byte[]> GetHeaderAsync(Client client, Address addr, CancellationToken cancellation)
        {
            var obj = await client.GetObjectHeader(cancellation, new ObjectHeaderParams() { Address = addr }, new CallOptions { Ttl = 2 });
            return Utility.StrictUTF8.GetBytes(obj.ToString());
        }

        private static async Task<byte[]> GetHashAsync(Client client, Address addr, string[] ps, CancellationToken cancellation)
        {
            if (ps.Length == 0 || ps[0] == "")
            {
                Object obj = await client.GetObjectHeader(cancellation, new ObjectHeaderParams() { Address = addr }, new CallOptions { Ttl = 2 });
                return obj.PayloadChecksum.Sum.ToByteArray();
            }
            Range range = ParseRange(ps[0]);
            List<byte[]> hashes = await client.GetObjectPayloadRangeHash(cancellation, new RangeChecksumParams() { Address = addr, Ranges = new List<Range>() { range }, Type = ChecksumType.Sha256, Salt = Array.Empty<byte>() }, new CallOptions { Ttl = 2 });
            if (hashes.Count == 0) throw new Exception("empty response, object range is invalid (expected 'Offset|Length')");
            return hashes[0];
        }

        private static Range ParseRange(string s)
        {
            string url = HttpUtility.UrlDecode(s);
            int sepIndex = url.IndexOf("|");
            if (sepIndex < 0) throw new Exception("object range is invalid (expected 'Offset|Length')");
            ulong offset = ulong.Parse(url[..sepIndex]);
            ulong length = ulong.Parse(url[(sepIndex + 1)..]);
            return new Range() { Offset = offset, Length = length };
        }
    }
}

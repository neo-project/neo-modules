using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using NeoFS.API.v2.Client;
using NeoFS.API.v2.Client.ObjectParams;
using NeoFS.API.v2.Cryptography;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Object = NeoFS.API.v2.Object.Object;
using Range = NeoFS.API.v2.Object.Range;

namespace Neo.Plugins
{
    class OracleNeoFSProtocol : IOracleProtocol
    {
        private readonly byte[] privateKey;
        private const string URIScheme = "neofs";
        private const string RangeCmd = "range";
        private const string HeaderCmd = "header";
        private const string HashCmd = "hash";

        public void Configure()
        {
        }

        public OracleNeoFSProtocol(Wallet wallet, ECPoint[] oracles)
        {
            privateKey = oracles.Select(p => wallet.GetAccount(p)).Where(p => p is not null && p.HasKey && !p.Lock).FirstOrDefault()?.GetKey().PrivateKey;
        }

        public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
        {
            Utility.Log(nameof(OracleNeoFSProtocol), LogLevel.Debug, $"Request: {uri.AbsoluteUri}");

            if (!Settings.Default.AllowPrivateHost)
            {
                IPHostEntry entry = await Dns.GetHostEntryAsync(uri.Host);
                if (entry.IsInternal())
                    return (OracleResponseCode.Forbidden, null);
            }
            try
            {
                byte[] res = Get(privateKey, uri, Settings.Default.NeoFS.EndPoint, cancellation);
                Utility.Log(nameof(OracleNeoFSProtocol), LogLevel.Debug, $"NeoFS result: {res.ToHexString()}");
                return (OracleResponseCode.Success, Convert.ToBase64String(res));
            }
            catch (Exception e)
            {
                Utility.Log(nameof(OracleNeoFSProtocol), LogLevel.Debug, $"NeoFS result: error,{e}");
                return (OracleResponseCode.Error, null);
            }
        }

        private byte[] Get(byte[] privateKey, Uri uri, string host, CancellationToken cancellation)
        {
            string[] ps = uri.PathAndQuery.TrimStart('/').Split("/");
            if (ps.Length == 0) throw new Exception("object ID is missing from URI");
            ContainerID containerID = ContainerID.FromBase58String(uri.OriginalString.Substring((URIScheme + "://").Length, uri.Host.Length));
            ObjectID objectID = ObjectID.FromBase58String(ps[0]);
            Address objectAddr = new Address()
            {
                ContainerId = containerID,
                ObjectId = objectID
            };
            Client client = new Client(privateKey.LoadPrivateKey(), host, 120000);
            if (ps.Length == 1)
            {
                return GetPayload(client, objectAddr, cancellation);
            }
            else if (ps[1] == RangeCmd)
            {
                return GetPayload(client, objectAddr, cancellation);
            }
            else if (ps[1] == HeaderCmd)
            {
                return GetPayload(client, objectAddr, cancellation);
            }
            else if (ps[1] == HashCmd)
            {
                return GetHash(cancellation, client, objectAddr, ps.Skip(2).ToArray());
            }
            else
            {
                throw new Exception("invalid command");
            }
        }

        private static byte[] GetPayload(Client client, Address addr, CancellationToken cancellation)
        {
            Object obj = client.GetObject(cancellation, new GetObjectParams() { Address = addr }, new CallOptions { Ttl = 2 }).Result;
            return obj.Payload.ToByteArray();
        }

        private byte[] GetRange(CancellationToken cancellation, Client client, Address addr, params string[] ps)
        {
            if (ps.Length == 0) throw new Exception("object range is invalid (expected 'Offset|Length'");
            Range range = ParseRange(ps[0]);
            return client.GetObjectPayloadRangeData(cancellation, new RangeDataParams() { Address = addr, Range = range }, new CallOptions { Ttl = 2 }).Result;
        }

        private byte[] GetHeader(CancellationToken cancellation, Client client, Address addr)
        {
            var obj = client.GetObjectHeader(cancellation, new ObjectHeaderParams() { Address = addr }, new CallOptions { Ttl = 2 });
            return obj.ToByteArray();
        }

        private byte[] GetHash(CancellationToken cancellation, Client client, Address addr, params string[] ps)
        {
            if (ps.Length == 0 || ps[0] == "")
            {
                Object obj = client.GetObjectHeader(cancellation, new ObjectHeaderParams() { Address = addr }, new CallOptions { Ttl = 2 });
                return obj.Header.PayloadHash.Sum.ToByteArray();
            }
            Range range = ParseRange(ps[0]);
            List<byte[]> hashes = client.GetObjectPayloadRangeHash(cancellation, new RangeChecksumParams() { Address = addr, Ranges = new List<Range>() { range } }, new CallOptions { Ttl = 2 });
            if (hashes.Count == 0) throw new Exception(string.Format("{0}: empty response", "object range is invalid (expected 'Offset|Length'"));
            return hashes[0];
        }

        private Range ParseRange(string s)
        {
            int sepIndex = s.IndexOf("|");
            if (sepIndex < 0) throw new Exception("object range is invalid (expected 'Offset|Length'");
            ulong offset = ulong.Parse(s[..sepIndex]);
            ulong length = ulong.Parse(s[sepIndex..]);
            return new Range() { Offset = offset, Length = length };
        }

        public void Dispose()
        {
        }
    }
}

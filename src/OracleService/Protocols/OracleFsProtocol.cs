using Google.Protobuf;
using Neo.Network.P2P.Payloads;
using NeoFS.API.v2.Client;
using NeoFS.API.v2.Client.ObjectParams;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Cryptography;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Range = NeoFS.API.v2.Object.Range;
using System.Collections.Generic;
using Object = NeoFS.API.v2.Object.Object;
using Neo.Wallets;
using Neo.Cryptography.ECC;

namespace Neo.Plugins
{
    class OracleFsProtocol : IOracleProtocol
    {
        private byte[] privateKey;
        private const string URIScheme = "neofs";
        private const string ErrInvalidScheme = "invalid URI scheme";
        private const string ErrMissingObject = "object ID is missing from URI";
        private const string ErrInvalidContainer = "container ID is invalid";
        private const string ErrInvalidObject = "object ID is invalid";
        private const string ErrInvalidRange = "object range is invalid (expected 'Offset|Length'";
        private const string ErrInvalidCommand = "invalid command";

        private const string RangeSep = "|";
        private const string RangeCmd = "range";
        private const string HeaderCmd = "header";
        private const string HashCmd = "hash";

        public void Configure()
        {
        }

        public void Dispose()
        {
        }

        public void AttachWallet(Wallet wallet, ECPoint[] oracles)
        {
            privateKey = oracles.Select(p => wallet.GetAccount(p)).Where(p => p is not null && p.HasKey && !p.Lock).FirstOrDefault(null)?.GetKey().PrivateKey;
        }

        public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
        {
            Utility.Log(nameof(OracleFsProtocol), LogLevel.Debug, $"Request: {uri.AbsoluteUri}");

            if (!Settings.Default.AllowPrivateHost)
            {
                IPHostEntry entry = await Dns.GetHostEntryAsync(uri.Host);
                if (entry.IsInternal())
                    return (OracleResponseCode.Forbidden, null);
            }
            int index = uri.LocalPath.GetHashCode() % Settings.Default.Fs.FSNodes.Length;
            try
            {
                byte[] res = Get(cancellation, privateKey, uri, Settings.Default.Fs.FSNodes[index]);
                Utility.Log(nameof(OracleFsProtocol), LogLevel.Debug, $"NeoFS result: {res.ToHexString()}");
                return (OracleResponseCode.Success, res.ToHexString());
            }
            catch (Exception e)
            {
                Utility.Log(nameof(OracleFsProtocol), LogLevel.Debug, $"NeoFS result: error,{e.Message}");
                return (OracleResponseCode.Error, null);
            }
        }

        private byte[] Get(CancellationToken cancellation, byte[] privateKey, Uri uri, string host)
        {
            if (uri.Scheme != URIScheme) throw new Exception(ErrInvalidScheme);
            string[] ps = uri.OriginalString.Substring((uri.Scheme + "://").Length).Split("/");
            if (ps.Length == 0) throw new Exception(ErrMissingObject);
            byte[] rawCID = Cryptography.Base58.Decode(uri.Host);
            if (rawCID.Length != 32) throw new Exception(ErrInvalidContainer);
            byte[] rawOID = Cryptography.Base58.Decode(ps[0]);
            if (rawOID.Length != 32) throw new Exception(ErrInvalidObject);
            ContainerID containerID = ContainerID.FromByteArray(rawCID);
            ObjectID objectID = ObjectID.FromByteArray(rawOID);
            Address objectAddr = new Address()
            {
                ContainerId = containerID,
                ObjectId = objectID
            };
            Client client = new Client(host, privateKey.LoadPrivateKey());
            if (ps.Length == 1)
            {
                return GetPayload(cancellation, client, objectAddr);
            }
            else if (ps[1] == RangeCmd)
            {
                return GetPayload(cancellation, client, objectAddr);
            }
            else if (ps[1] == HeaderCmd)
            {
                return GetPayload(cancellation, client, objectAddr);
            }
            else if (ps[1] == HashCmd)
            {
                return GetHash(cancellation, client, objectAddr, ps.Skip(2).ToArray());
            }
            else
            {
                throw new Exception(ErrInvalidCommand);
            }
        }

        private byte[] GetPayload(CancellationToken cancellation, Client client, Address addr)
        {
            Object obj = client.GetObject(cancellation, new GetObjectParams() { Address = addr }).Result;
            return obj.Payload.ToByteArray();
        }

        private byte[] GetRange(CancellationToken cancellation, Client client, Address addr, params string[] ps)
        {
            if (ps.Length == 0) throw new Exception(ErrInvalidRange);
            Range range = ParseRange(ps[0]);
            return client.GetObjectPayloadRangeData(cancellation, new RangeDataParams() { Address = addr, Range = range }).Result;
        }

        private byte[] GetHeader(CancellationToken cancellation, Client client, Address addr)
        {
            var obj = client.GetObjectHeader(cancellation, new ObjectHeaderParams() { Address = addr });
            return obj.ToByteArray();
        }

        private byte[] GetHash(CancellationToken cancellation, Client client, Address addr, params string[] ps)
        {
            if (ps.Length == 0 || ps[0] == "")
            {
                Object obj = client.GetObjectHeader(cancellation, new ObjectHeaderParams() { Address = addr });
                return obj.Header.PayloadHash.Sum.ToByteArray();
            }
            Range range = ParseRange(ps[0]);
            List<byte[]> hashes = client.GetObjectPayloadRangeHash(cancellation, new RangeChecksumParams() { Address = addr, Ranges = new List<Range>() { range } });
            if (hashes.Count == 0) throw new Exception(string.Format("{0}: empty response", ErrInvalidRange));
            return hashes[0];
        }

        private Range ParseRange(string s)
        {
            int sepIndex = s.IndexOf(RangeSep);
            if (sepIndex < 0) throw new Exception(ErrInvalidRange);
            ulong offset = ulong.Parse(s.Substring(0, sepIndex));
            ulong length = ulong.Parse(s.Substring(sepIndex));
            return new Range() { Offset = offset, Length = length };
        }
    }
}

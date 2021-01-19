using Neo.FSNode.Services.Policer;
using Google.Protobuf;
using Neo.IO.Data.LevelDB;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using V2Object = NeoFS.API.v2.Object.Object;
using static NeoFS.API.v2.Object.SearchRequest.Types.Body.Types;

namespace Neo.FSNode.LocalObjectStorage.MetaBase
{
    public class Db
    {
        private static readonly byte[] PrimaryBucketPrefix = Encoding.UTF8.GetBytes("objects");
        private static readonly byte[] IndexBucketPrefix = Encoding.UTF8.GetBytes("index");
        private static readonly byte[] TombstoneBucketPrefix = Encoding.UTF8.GetBytes("tombstones");

        private string path;
        private Cfg cfg;
        private DB db;
        private Dictionary<MatchType, Matcher> matchers;


        private bool UnknownMatcher(string key, string s1 = null, string s2 = null)
        {
            switch (key)
            {
                case "$Object:PHY":
                case "$Object:ROOT":
                    return true;
                default:
                    return false;
            }
        }

        private bool StringEqualMatcher(string key, string objVal, string filterVal)
        {
            switch (key)
            {
                case "$Object:PHY":
                case "$Object:ROOT":
                    return true;
                default:
                    return objVal == filterVal;
            }
        }

        public Db(Option[] opts)
        {
            var c = new Cfg();
            foreach (var opt in opts)
            {
                opt(c);
            }

            this.path = ""; // TODO, maybe create boltDb c# version later
            this.cfg = c;
            this.matchers = new Dictionary<MatchType, Matcher>
            {
                { MatchType.Unspecified, this.UnknownMatcher },
                {MatchType.StringEqual, this.StringEqualMatcher }
            };
        }

        public V2Object Get(Address addr)
        {
            var addrKey = ConcatKey(PrimaryBucketPrefix, Encoding.UTF8.GetBytes(addr.ToString()));
            if (!this.db.Contains(ReadOptions.Default, addrKey))
                throw new ArgumentException("object not found");
            var data = this.db.Get(ReadOptions.Default, addrKey);
            return V2Object.Parser.ParseFrom(data);
        }

        public void Put(V2Object obj)
        {
            var data = obj.ToByteArray();
            var addr = new Address() { ObjectId = obj.ObjectId, ContainerId = obj.Header.ContainerId };
            var priKey = ConcatKey(PrimaryBucketPrefix, AddressToKeyBytes(addr));
            var par = obj.Header.Split.Parent is null ? false : true;

            // put into primary bucket, key = priPrefix + addr, value = obj
            if (!par)
                this.db.Put(WriteOptions.Default, priKey, data);

            var indices = ObjectIndices(obj, par);
            // put into indice bucket, key = indexPrefix + indice.key + indice.value + addr, value = null?
            foreach (var indice in indices)
            {
                var indiceKey = IndexBucketPrefix.Concat(Encoding.UTF8.GetBytes(indice.Key))
                    .Concat(NonEmptyKeyBytes(Encoding.UTF8.GetBytes(indice.Value))).Concat(AddressToKeyBytes(addr)).ToArray();
                this.db.Put(WriteOptions.Default, indiceKey, new byte[0]);
            }
        }

        private KeyValuePair<string, string>[] ObjectIndices(V2Object obj, bool parent)
        {
            var attr = obj.Header.Attributes;
            var res = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>(Filter.FilterHeaderVersion, obj.Header.Version.ToString()), // FilterHeaderVersion
                new KeyValuePair<string, string>(Filter.FilterHeaderContainerID, obj.Header.ContainerId.ToString()),
                new KeyValuePair<string, string>(Filter.FilterHeaderOwnerID, obj.Header.OwnerId.ToString()),
                new KeyValuePair<string, string>(Filter.FilterHeaderParent, obj.Header.Split.Parent.ToString()),
                new KeyValuePair<string, string>(Filter.FilterHeaderObjectID, obj.ObjectId.ToString()),
                // TODO: add remaining fields after neofs-api#72
            };

            if (obj.Header.ObjectType == ObjectType.Regular && obj.Header.Split.Parent is null)
                res = res.Append(new KeyValuePair<string, string>(Filter.FilterPropertyRoot, null)).ToArray();

            if (!parent)
                res = res.Append(new KeyValuePair<string, string>(Filter.FilterPropertyPhy, null)).ToArray();

            foreach (var a in attr)
            {
                res = res.Append(new KeyValuePair<string, string>(a.Key, a.Value)).ToArray();
            }

            return res;
        }

        public void Delete(Address addr)
        {
            this.Del(addr);
        }

        public void DeleteObjects(Address[] addrs)
        {
            this.Del(addrs);
        }

        private void Del(params Address[] addrs)
        {
            // put into tombstone bucket
            foreach (var addr in addrs)
            {
                var key = ConcatKey(TombstoneBucketPrefix, AddressToKeyBytes(addr));
                this.db.Put(WriteOptions.Default, key, new byte[0]);
            }
        }

        private bool ObjectRemoved(byte[] addr)
        {
            var key = ConcatKey(TombstoneBucketPrefix, addr);
            return this.db.Contains(ReadOptions.Default, key);
        }

        public Address[] Select(SearchFilters fs)
        {
            if (fs.Filters.Length == 0)
                return this.SelectAll();

            // keep processed addresses
            // value equal to number (index+1) of latest matched filter
            Dictionary<string, int> mAddr = new Dictionary<string, int>();
            var fLen = fs.Filters.Length;
            var res = new List<Address>();

            // keep processed addresses
            // value equal to number (index+1) of latest matched filter
            // from indexed bucket
            for (int i = 0; i < fLen; i++)
            {
                var f = fs.Filters[i];
                var matchFunc = this.matchers[f.MatchType];
                var key = f.Key;
                var fval = f.Value;
                var target = ConcatKey(IndexBucketPrefix, Encoding.UTF8.GetBytes(key));
                using (Iterator it = db.NewIterator(ReadOptions.Default))
                {
                    for (it.Seek(target); it.Valid(); it.Next())
                    {
                        var k = it.Key();
                        if (k.Length < 1 || k.AsSpan().SequenceCompareTo(target) < 0) break;
                        var include = matchFunc(key, Encoding.UTF8.GetString(CutKeyBytes(SeparateKey(target, k))), fval);

                        if (include)
                        {
                            using (Iterator it2 = db.NewIterator(ReadOptions.Default))
                            {
                                for (it2.Seek(k); it2.Valid(); it2.Next())
                                {
                                    var k2 = it2.Key();
                                    if (k2.Length < 1 || k2.AsSpan().SequenceCompareTo(target) < 0) break;
                                    var num = mAddr[Encoding.UTF8.GetString(k2)];
                                    if (num == i)
                                        mAddr[Encoding.UTF8.GetString(k2)] = i + 1;
                                }
                            }
                        }
                    }
                }
            }

            foreach (var item in mAddr)
            {
                if (item.Value != fLen) continue;
                if (ObjectRemoved(Encoding.UTF8.GetBytes(item.Key))) continue;

                res.Add(Address.ParseString(item.Key));
            }

            return res.ToArray();
        }

        public Address[] SelectAll()
        {
            var addrs = new List<Address>();
            var result = new List<byte[]>();

            // for primary bucket
            using (Iterator it = db.NewIterator(ReadOptions.Default))
            {
                for (it.Seek(PrimaryBucketPrefix); it.Valid(); it.Next())
                {
                    var key = it.Key(); // prefix + Encoding.UTF8.GetBytes(addr.ToString())
                    if (key.Length < 1 || key.AsSpan().SequenceCompareTo(PrimaryBucketPrefix) < 0) break;
                    result.Add(SeparateKey(PrimaryBucketPrefix, key));
                }
            }

            // for index bucket, only select FilterPropertyRoot
            // indexPrefix + FilterPropertyRoot
            using (Iterator it = db.NewIterator(ReadOptions.Default))
            {
                var target = ConcatKey(IndexBucketPrefix, Encoding.UTF8.GetBytes(Filter.FilterPropertyRoot));
                for (it.Seek(target); it.Valid(); it.Next())
                {
                    var key = it.Key(); // key = indexPrefix + indice.key + indice.value + addr
                    if (key.Length < 1 || key.AsSpan().SequenceCompareTo(target) < 0) break;
                    var addrBytes = CutKeyBytes(SeparateKey(Encoding.UTF8.GetBytes(Filter.FilterPropertyRoot), SeparateKey(IndexBucketPrefix, key)));
                    result.Add(addrBytes);
                }
            }

            foreach (var r in result)
            {
                if (ObjectRemoved(r))
                    continue;
                addrs.Add(KeyBytesToAddress(r));
            }

            return addrs.ToArray();
        }

        private byte[] ConcatKey(byte[] prefix, byte[] k)
        {
            if (prefix is null) return k;
            if (k is null) return prefix;
            return prefix.Concat(k).ToArray();
        }

        private byte[] SeparateKey(byte[] prefix, byte[] k)
        {
            if (prefix is null) return k;
            if (k is null) return null;
            return k.Skip(prefix.Length).ToArray();
        }

        private byte[] NonEmptyKeyBytes(byte[] key)
        {
            return ConcatKey(new byte[] { 0x00 }, key);
        }

        private byte[] CutKeyBytes(byte[] key)
        {
            return SeparateKey(new byte[] { 0x00 }, key);
        }

        private byte[] AddressToKeyBytes(Address addr)
        {
            return Encoding.UTF8.GetBytes(addr.ToString());
        }

        private Address KeyBytesToAddress(byte[] key)
        {
            return Address.ParseString(Encoding.UTF8.GetString(key));
        }
    }



    public delegate bool Matcher(string a, string b, string c);

    public class Cfg
    {
        private DB db;
        //public DB DB { get; set; }

    }

    public delegate void Option(Cfg cfg);
}

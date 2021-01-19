using Google.Protobuf;
using Neo.FSNode.LocalObjectStorage.Bucket;
using Neo.FSNode.LocalObjectStorage.MetaBase;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.LocalObjectStorage.LocalStore
{
    public class Storage
    {
        //private IBucket metaBucket;
        private Db metaBase;
        private IBucket blobBucket;

        public Storage(IBucket blob, Db meta, params Option[] opts)
        {
            var cfg = Cfg.DefaultCfg;
            foreach (var opt in opts)
            {
                opt(cfg);
            }

            this.blobBucket = blob;
            this.metaBase = meta;
        }

        public void Put(V2Object obj)
        {
            var addrBytes = obj.Address().ToByteArray();
            var objBytes = obj.ToByteArray();

            this.blobBucket.Set(addrBytes, objBytes);
            this.metaBase.Put(obj.CutPayload());
        }

        public void Delete(Address addr)
        {
            var addrBytes = addr.ToByteArray();
            this.blobBucket.Del(addrBytes);
            this.metaBase.Delete(addr);
        }

        public V2Object Get(Address addr)
        {
            var addrBytes = addr.ToByteArray();
            var objBytes = this.blobBucket.Get(addrBytes);

            return V2Object.Parser.ParseFrom(objBytes);
        }

        public V2Object Head(Address addr)
        {
            return this.metaBase.Get(addr);
        }

        public Address[] Select(SearchFilters fs)
        {
            return this.metaBase.Select(fs);
        }
    }

    public delegate void Option(Cfg cfg);

    public class TempLogger
    {
        public Option WithLogger(TempLogger l)
        {
            return c =>
            {
                if (!(l is null))
                    c.logger = l;
            };
        }
    }

    public class Cfg
    {
        public TempLogger logger;
        public static Cfg DefaultCfg = new Cfg { logger = new TempLogger() };
    }
}

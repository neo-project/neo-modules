using Google.Protobuf;
using Neo.FileStorage.LocalObjectStorage.Bucket;
using Neo.FileStorage.LocalObjectStorage.MetaBase;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.LocalStore
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

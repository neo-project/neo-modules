using Google.Protobuf;
using Neo.Fs.LocalObjectStorage.Bucket;
using NeoFS.API.v2.Refs;
using FsObject = NeoFS.API.v2.Object.Object;

namespace Neo.Fs.LocalObjectStorage.LocalStore
{
    public class Storage
    {
        private TempLogger logger;
        private IBucket metaBucket;
        private IBucket blobBucket;

        public Storage(IBucket blob, IBucket meta, params Option[] opts)
        {
            var cfg = Cfg.DefaultCfg;
            foreach (var opt in opts)
            {
                opt(cfg);
            }

            this.blobBucket = blob;
            this.metaBucket = meta;
            this.logger = cfg.logger;
        }

        public void Put(FsObject obj)
        {
            var addrBytes = obj.Address().ToByteArray();
            var objBytes = obj.ToByteArray();
            var metaBytes = ObjectMeta.MetaFromObject(obj).MetaToBytes();

            this.blobBucket.Set(addrBytes, objBytes);
            this.metaBucket.Set(addrBytes, metaBytes);
        }

        public void Delete(Address addr)
        {
            var addrBytes = addr.ToByteArray();
            this.blobBucket.Del(addrBytes);
            this.metaBucket.Del(addrBytes);
        }

        public FsObject Get(Address addr)
        {
            var addrBytes = addr.ToByteArray();
            var objBytes = this.blobBucket.Get(addrBytes);

            return FsObject.Parser.ParseFrom(objBytes);
        }

        public ObjectMeta Head(Address addr)
        {
            var addrBytes = addr.ToByteArray();
            var metaBytes = this.metaBucket.Get(addrBytes);

            return ObjectMeta.MetaFromBytes(metaBytes);
        }

        public delegate bool StorageHandler(ObjectMeta meta);

        public void Iterate(IFilterPipeline filter, StorageHandler handler)
        {
            if (filter is null)
            {
                filter = new FilterPipeline(new FilterParam()
                {
                    Name = "SKIPPING_FILTER",
                    FilterFunc = (ctx, meta) => { return FilterResult.FrPass; }
                });
            }

            this.metaBucket.Iterate((k, v) =>
            {
                var meta = ObjectMeta.MetaFromBytes(v);
                if (filter.Pass(new WrapperContext(), meta).C == FilterCode.CodePass)
                    return !handler(meta);
                return true;
            });
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

using Neo.FileStorage.API.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Object.Util;
using System.Collections.Generic;
using Neo.FileStorage.Services.Object.Get.Writer;
using V2Object = Neo.FileStorage.API.Object.Object;
using V2Range = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Services.Object.Get
{
    public partial class GetService
    {
        public KeyStorage KeyStorage { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public ClientCache ClientCache { get; init; }
        public ITraverserGenerator TraverserGenerator { get; init; }

        public void Get(GetPrm prm)
        {
            Get(prm, null, false);
        }

        public V2Object Head(HeadPrm prm)
        {
            var writer = new SimpleObjectWriter();
            prm.Writer = writer;
            Get(prm, null, true);
            return writer.Obj;
        }

        public void GetRange(RangePrm prm)
        {
            Get(prm, prm.Range, false);
        }

        public List<byte[]> GetRangeHash(RangeHashPrm prm)
        {
            var hashes = new List<byte[]>();
            foreach (var range in prm.Ranges)
            {
                var writer = new RangeHashWriter(prm.HashType);
                var range_prm = new RangePrm
                {
                    Range = range,
                    Raw = prm.Raw,
                    Writer = writer,
                };
                range_prm.WithCommonPrm(prm);
                Get(range_prm, range, false);
                hashes.Add(writer.GetHash());
            }
            return hashes;
        }

        internal void Get(GetCommonPrm prm, V2Range range, bool head_only)
        {
            var executor = new Executor
            {
                Prm = prm,
                Range = range,
                HeadOnly = head_only,
                GetService = this,
            };
            executor.Execute();
        }
    }
}

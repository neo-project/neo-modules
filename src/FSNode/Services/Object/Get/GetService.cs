using NeoFS.API.v2.Object;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System.Collections.Generic;
using Neo.FSNode.Services.Object.Get.Writer;
using V2Range = NeoFS.API.v2.Object.Range;

namespace Neo.FSNode.Services.Object.Get
{
    public class GetService
    {
        public Storage LocalStorage;
        public ClientCache ClientCache;
        public ITraverserGenerator TraverserGenerator;

        public void Get(GetPrm prm)
        {
            Get(prm, null, false);
        }

        public void Head(HeadPrm prm)
        {
            Get(prm, null, true);
        }

        public void GetRange(RangePrm prm)
        {
            Get(prm, prm.Range, false);
        }

        public GetRangeHashResponse GetRangeHash(RangeHashPrm prm)
        {
            var hashes = new List<byte[]>();
            foreach (var range in prm.Ranges)
            {
                var writer = new RangeHashWriter(prm.HashType);
                var range_prm = new RangePrm
                {
                    Range = range,
                    Raw = prm.Raw,
                    ChunkWriter = writer,
                };
                range_prm.WithCommonPrm(prm);
                Get(range_prm, range, false);
                hashes.Add(writer.GetHash());
            }
            return Responser.GetRangeHashResponse(hashes);
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

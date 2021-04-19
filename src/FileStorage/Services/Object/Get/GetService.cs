using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Get.Execute;
using Neo.FileStorage.Services.Object.Get.Writer;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.Reputaion;
using System.Collections.Generic;
using System.Linq;
using FSRange = Neo.FileStorage.API.Object.Range;


namespace Neo.FileStorage.Services.Object.Get
{
    public partial class GetService
    {
        //Default value should be true
        public bool Assemble { get; init; }
        public KeyStorage KeyStorage { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public ReputaionClientCache ClientCache { get; init; }
        public Client MorphClient { get; init; }
        public ITraverserGenerator TraverserGenerator { get; init; }

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
            List<byte[]> hashes = new();
            foreach (var range in prm.Ranges)
            {
                var writer = new RangeHashGenerator(prm.HashType);
                var range_prm = new RangePrm
                {
                    Range = range,
                    Writer = writer,
                };
                range_prm.WithGetCommonPrm(prm);
                Get(range_prm, range, false);
                hashes.Add(writer.GetHash());
            }
            GetRangeHashResponse resp = new();
            resp.Body = new();
            resp.Body.HashList.AddRange(hashes.Select(p => ByteString.CopyFrom(p)));
            return resp;
        }

        internal void Get(GetCommonPrm prm, FSRange range, bool head_only)
        {
            RangePrm range_prm = new();
            range_prm.WithGetCommonPrm(prm);
            range_prm.Range = range;
            var executor = new ExecuteContext
            {
                Prm = range_prm,
                Range = range,
                HeadOnly = head_only,
                GetService = this,
            };
            executor.Execute();
        }
    }
}

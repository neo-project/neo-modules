using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Services.Object.Get.Execute;
using Neo.FileStorage.Storage.Services.Object.Get.Remote;
using Neo.FileStorage.Storage.Services.Object.Get.Writer;
using Neo.FileStorage.Storage.Services.Object.Util;
using FSRange = Neo.FileStorage.API.Object.Range;


namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public partial class GetService
    {
        public bool Assemble { get; init; } //Default value should be true
        public KeyStore KeyStorage { get; init; }
        public ILocalObjectSource LocalStorage { get; init; }
        public IGetClientCache ClientCache { get; init; }
        public IEpochSource EpochSource { get; init; }
        public ITraverserGenerator TraverserGenerator { get; init; }

        public void Get(GetPrm prm, CancellationToken token)
        {
            Get(prm, null, false, token);
        }


        public void Head(HeadPrm prm, CancellationToken token)
        {
            Get(prm, null, true, token);
        }

        public void GetRange(RangePrm prm, CancellationToken token)
        {
            Get(prm, prm.Range, false, token);
        }

        public GetRangeHashResponse GetRangeHash(RangeHashPrm prm, CancellationToken token)
        {
            List<byte[]> hashes = new();
            foreach (var range in prm.Ranges)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException();
                var writer = new RangeHashGenerator(prm.HashType);
                var range_prm = new RangePrm
                {
                    Range = range,
                    Writer = writer,
                };
                range_prm.WithGetCommonPrm(prm);
                Get(range_prm, range, false, token);
                hashes.Add(writer.GetHash());
            }
            GetRangeHashResponse resp = new();
            resp.Body = new();
            resp.Body.HashList.AddRange(hashes.Select(p => ByteString.CopyFrom(p)));
            return resp;
        }

        internal void Get(GetCommonPrm prm, FSRange range, bool head_only, CancellationToken token)
        {
            RangePrm range_prm = new();
            range_prm.WithGetCommonPrm(prm);
            range_prm.Range = range;
            var executor = new ExecuteContext
            {
                Token = token,
                Prm = range_prm,
                Range = range,
                HeadOnly = head_only,
                GetService = this,
            };
            executor.Execute();
        }
    }
}

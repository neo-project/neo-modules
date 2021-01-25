using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Util;

namespace Neo.FSNode.Services.Object.Get
{
    public class GetCommonPrm : CommonPrm
    {
        public Address Address;
        public bool Raw;
        public IHeaderWriter HeaderWriter;
        public IChunkWriter ChunkWriter;

        public void WithGetCommonPrm(GetCommonPrm prm)
        {
            Address = prm.Address;
            Raw = prm.Raw;
            HeaderWriter = prm.HeaderWriter;
            ChunkWriter = prm.ChunkWriter;
            WithCommonPrm(prm);
        }
    }
}

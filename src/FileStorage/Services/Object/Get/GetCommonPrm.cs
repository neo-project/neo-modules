using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Get
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

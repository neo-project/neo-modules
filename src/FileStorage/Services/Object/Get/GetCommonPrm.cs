using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Get
{
    public class GetCommonPrm : CommonPrm
    {
        public Address Address;
        public bool Raw;
        public IObjectResponseWriter Writer;

        public void WithGetCommonPrm(GetCommonPrm prm)
        {
            Address = prm.Address;
            Raw = prm.Raw;
            Writer = prm.Writer;
            WithCommonPrm(prm);
        }
    }
}

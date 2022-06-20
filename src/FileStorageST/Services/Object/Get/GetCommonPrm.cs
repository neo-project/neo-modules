using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Util;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public class GetCommonPrm : CommonPrm
    {
        public Address Address;
        public bool Raw;
        public IObjectResponseWriter Writer;
        public Forwarder Forwarder;

        public void WithGetCommonPrm(GetCommonPrm prm)
        {
            Address = prm.Address;
            Raw = prm.Raw;
            Writer = prm.Writer;
            Forwarder = prm.Forwarder;
            WithCommonPrm(prm);
        }
    }
}

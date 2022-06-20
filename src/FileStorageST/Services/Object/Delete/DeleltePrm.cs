using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Delete
{
    public class DeletePrm : CommonPrm
    {
        public Address Address;
        public ITombstoneWriter Writer;
        public static DeletePrm FromRequest(DeleteRequest request)
        {
            var address = request.Body?.Address;
            AddressCheck(address);
            var prm = new DeletePrm
            {
                Address = address,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}

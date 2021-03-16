using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Delete
{
    public class DeletePrm : CommonPrm
    {
        public Address Address;
        public ITombstoneWriter Writer;
        public static DeletePrm FromRequest(DeleteRequest request)
        {
            var prm = new DeletePrm
            {
                Address = request.Body.Address,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}


using Neo.FileStorage.API.Object;

namespace Neo.FileStorage.Services.Object
{
    public interface IPutRequestStream
    {
        void Send(PutRequest request);
        PutResponse Close();
    }
}

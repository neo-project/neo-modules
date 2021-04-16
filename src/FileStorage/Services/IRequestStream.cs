
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Services
{
    public interface IRequestStream
    {
        void Send(IRequest request);
        IResponse Close();
    }
}

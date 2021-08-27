using Neo.FileStorage.API.Session;
using System;

namespace Neo.FileStorage.Storage.Services
{
    public interface IRequestStream : IDisposable
    {
        void Send(IRequest request);
        IResponse Close();
    }
}

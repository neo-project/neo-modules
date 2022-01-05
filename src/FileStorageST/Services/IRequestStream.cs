using Neo.FileStorage.API.Session;
using System;

namespace Neo.FileStorage.Storage.Services
{
    public interface IRequestStream : IDisposable
    {
        bool Send(IRequest request);
        IResponse Close();
    }
}

using System;

namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IIterator
    {
        void Iterate(Action<Trust> handler);
    }
}

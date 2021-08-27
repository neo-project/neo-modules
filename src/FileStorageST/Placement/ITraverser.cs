using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Placement
{
    public interface ITraverser
    {
        List<List<Network.Address>> Next();
        void SubmitSuccess();
        bool Success();
    }
}

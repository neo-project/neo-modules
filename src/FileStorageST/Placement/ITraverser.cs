using Neo.FileStorage.API.Netmap;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Placement
{
    public interface ITraverser
    {
        List<Node> Next();
        void SubmitSuccess();
        bool Success();
    }
}

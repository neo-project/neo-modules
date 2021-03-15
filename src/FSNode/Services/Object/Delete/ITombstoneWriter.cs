
using Akka.Actor;

using V2Address = NeoFS.API.v2.Refs.Address;

namespace Neo.FSNode.Services.Object.Delete
{
    public interface ITombstoneWriter
    {
        void SetAddress(V2Address address);
    }
}

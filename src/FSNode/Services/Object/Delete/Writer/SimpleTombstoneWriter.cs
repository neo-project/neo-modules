using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Services.Object.Delete.Writer
{
    public class SimpleTombstoneWriter
    {
        public Address Address { get; private set; }

        public void SetAddress(Address address)
        {
            Address = address;
        }
    }
}

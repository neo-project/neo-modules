using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Services.Object.Delete.Writer
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

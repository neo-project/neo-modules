
namespace Neo.FSNode.Services.Object.RangeHash
{
    public interface IHasher
    {
        void Add(byte[] data);
        byte[] Sum();
    }
}

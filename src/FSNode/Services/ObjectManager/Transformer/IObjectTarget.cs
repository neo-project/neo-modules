using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.ObjectManager.Transformer
{
    public interface IObjectTarget
    {
        void WriteHeader(V2Object obj);
        void WriteChunk(byte[] chunk);
        AccessIdentifiers Close();
    }
}

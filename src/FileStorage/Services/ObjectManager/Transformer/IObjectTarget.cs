using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.ObjectManager.Transformer
{
    public interface IObjectTarget
    {
        void WriteHeader(V2Object obj);
        void WriteChunk(byte[] chunk);
        AccessIdentifiers Close();
    }
}

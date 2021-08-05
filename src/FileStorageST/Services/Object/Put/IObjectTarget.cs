using Neo.FileStorage.Storage.Services.Object.Put.Target;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public interface IObjectTarget
    {
        void WriteHeader(FSObject obj);
        void WriteChunk(byte[] chunk);
        AccessIdentifiers Close();
    }
}

using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Services.Session
{
    public interface ITokenStorage
    {
        PrivateToken Get(OwnerID owner, byte[] token);
    }
}

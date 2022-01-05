using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Session.Storage;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Session
{
    public interface ITokenStorage
    {
        PrivateToken Get(OwnerID owner, byte[] token);
    }
}

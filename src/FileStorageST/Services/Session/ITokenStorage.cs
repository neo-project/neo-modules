using System.Security.Cryptography;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Services.Session
{
    public interface ITokenStorage
    {
        ECDsa Get(OwnerID owner, byte[] token);
    }
}

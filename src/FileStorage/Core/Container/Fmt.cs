using System;
using V2Container = Neo.FileStorage.API.Container.Container;
using V2Version = Neo.FileStorage.API.Refs.Version;

namespace Neo.FileStorage.Core.Container
{
    public static class Helper
    {
        public static void CheckFormat(V2Container c)
        {
            if (c.PlacementPolicy == null)
                throw new ArgumentException("placement policy is null");

            if (c.Version == null)
                throw new ArgumentException("version is null");

            if (!V2Version.IsSupportedVersion(c.Version))
                throw new ArgumentException("incorrect version");

            if (c.OwnerId == null)
                throw new ArgumentException("owner id is null");

            if (c.OwnerId.Value.Length != 25) //NEO3WalletSize
                throw new ArgumentException("incorrect owner identifier value length");

            if (!Guid.TryParse(c.Nonce.ToByteArray().ToHexString(), out Guid g))
                throw new ArgumentException("incorrect nonce");
        }
    }
}

using System;
using V2Container = NeoFS.API.v2.Container.Container;
using V2Version = NeoFS.API.v2.Refs.Version;

namespace Neo.FSNode.Core.Container
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

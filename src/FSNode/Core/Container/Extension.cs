using V2Container = NeoFS.API.v2.Container;
using NeoFS.API.v2.Refs;
using System;

namespace Neo.FSNode.Core.Container
{
    public static class Extension
    {
        public static bool CheckFormat(this V2Container.Container container)
        {
            if (container.PlacementPolicy is null) return false;
            if (!NeoFS.API.v2.Refs.Version.IsSupportedVersion(container.Version)) return false;
            if (container.OwnerId.Value.Length != OwnerID.ValueSize) return false;
            try
            {
                var guid = new Guid(container.Nonce.ToByteArray());
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }
    }
}

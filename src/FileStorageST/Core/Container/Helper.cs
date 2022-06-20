using Neo.FileStorage.API.Refs;
using System;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Core.Container
{
    public static class Helper
    {
        public const string ContainerNotFoundError = "container not found";

        public static bool CheckFormat(this FSContainer container)
        {
            if (container.PlacementPolicy is null) return false;
            if (!API.Refs.Version.IsSupportedVersion(container.Version)) return false;
            if (container.OwnerId?.Value?.Length != OwnerID.ValueSize) return false;
            try
            {
                var guid = container.NonceUUID;
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }
    }
}

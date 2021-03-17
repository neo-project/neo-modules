using Neo.FileStorage.API.Refs;
using V2Container = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Core.Container
{
    // Source is an interface that wraps
    // basic container receiving method.
    public interface ISource
    {
        // Get reads the container from the storage by identifier.
        // It returns the pointer to requested container and any error encountered.
        //
        // Get must return exactly one non-nil value.
        // Get must return ErrNotFound if the container is not in storage.
        //
        // Implementations must not retain the container pointer and modify
        // the container through it.
        V2Container Get(ContainerID id);
    }
}

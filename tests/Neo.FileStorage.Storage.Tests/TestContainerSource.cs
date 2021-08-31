using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Core.Container;
using System;
using System.Collections.Generic;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Tests
{
    public class TestContainerSource : IContainerSoruce
    {
        public Dictionary<ContainerID, FSContainer> Containers = new();

        public FSContainer GetContainer(ContainerID cid)
        {
            if (Containers.TryGetValue(cid, out var container))
                return container;
            throw new InvalidOperationException("could not get container info");
        }
    }
}

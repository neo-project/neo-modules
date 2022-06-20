using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Core.Container;
using System;
using System.Collections.Generic;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Tests
{
    public class TestContainerSource : IContainerSource
    {
        public Dictionary<ContainerID, ContainerWithSignature> Containers = new();

        public ContainerWithSignature GetContainer(ContainerID cid)
        {
            if (Containers.TryGetValue(cid, out var containerWithSignature))
                return containerWithSignature;
            throw new InvalidOperationException("could not get container info");
        }
    }
}

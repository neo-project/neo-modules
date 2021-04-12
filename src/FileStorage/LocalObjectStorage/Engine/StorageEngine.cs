using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.Engine
{
    public class StorageEngine
    {
        public FSObject Get(Address address)
        {
            throw new NotImplementedException();
        }

        public void Put(FSObject obj)
        {
            throw new NotImplementedException();
        }

        public List<Address> Select(SearchFilters filters)
        {
            throw new NotImplementedException();
        }

        public List<ContainerID> ListContainers()
        {
            throw new NotImplementedException();
        }
    }
}

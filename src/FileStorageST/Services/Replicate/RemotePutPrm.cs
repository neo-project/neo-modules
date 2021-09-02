using FSObject = Neo.FileStorage.API.Object.Object;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public class RemotePutPrm
    {
        public List<Network.Address> Addresses;
        public FSObject Object;
    }
}

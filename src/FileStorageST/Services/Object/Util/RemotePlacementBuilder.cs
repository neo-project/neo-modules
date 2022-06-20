using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Network;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Reputation;
using System;
using System.Collections.Generic;
using System.Linq;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class RemotePlacementBuilder : NetworkMapBuilder
    {
        private readonly ILocalInfoSource localInfoSource;

        public RemotePlacementBuilder(INetmapSource netmapSource, ILocalInfoSource localInfoSource)
        : base(netmapSource)
        {
            this.localInfoSource = localInfoSource;
        }

        public override List<List<Node>> BuildPlacement(FSAddress address, PlacementPolicy policy)
        {
            var nss = base.BuildPlacement(address, policy);
            foreach (var ns in nss.ToList())
            {
                foreach (var n in ns.ToList())
                {
                    if (localInfoSource.PublicKey.SequenceEqual(n.PublicKey))
                        ns.Remove(n);
                }
            }
            return nss;
        }
    }
}

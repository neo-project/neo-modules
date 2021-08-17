using System;
using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Network;
using Neo.FileStorage.Placement;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class RemotePlacementBuilder : NetworkMapBuilder
    {
        private readonly ILocalInfoSource localAddressesSource;

        public RemotePlacementBuilder(MorphInvoker invoker, ILocalInfoSource localInfoSource)
        : base(invoker)
        {
            localAddressesSource = localInfoSource;
        }

        public override List<List<Node>> BuildPlacement(FSAddress address, PlacementPolicy policy)
        {
            var node_list = base.BuildPlacement(address, policy);
            foreach (var ns in node_list)
            {
                foreach (var n in ns)
                {
                    List<Address> addrs;
                    try
                    {
                        addrs = n.NetworkAddresses.Select(p => Address.FromString(p)).ToList();
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(RemotePlacementBuilder), LogLevel.Error, e.Message);
                        continue;
                    }
                    if (addrs.Intersect(localAddressesSource.Addresses).Any())
                        ns.Remove(n);
                }
            }
            return node_list;
        }
    }
}

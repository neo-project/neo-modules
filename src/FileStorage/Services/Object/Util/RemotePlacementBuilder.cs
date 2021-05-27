using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network;
using Neo.FileStorage.Services.ObjectManager.Placement;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Services.Object.Util
{
    public class RemotePlacementBuilder : NetworkMapBuilder
    {
        private readonly Address localAddress;

        public RemotePlacementBuilder(Client client, Address address)
        : base(client)
        {
            localAddress = address;
        }

        public override List<List<Node>> BuildPlacement(FSAddress address, PlacementPolicy policy)
        {
            var node_list = base.BuildPlacement(address, policy);
            foreach (var ns in node_list)
            {
                foreach (var n in ns)
                {
                    Address addr;
                    try
                    {
                        addr = Address.FromString(n.NetworkAddress);
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(RemotePlacementBuilder), LogLevel.Error, e.Message);
                        continue;
                    }
                    if (addr == localAddress)
                        ns.Remove(n);
                }
            }
            return null;
        }
    }
}

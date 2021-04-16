
using System;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Reputaion.Storage;
using static Neo.FileStorage.Network.Address;
using static Neo.Utility;
using MorphClient = Neo.FileStorage.Morph.Invoker.Client;

namespace Neo.FileStorage.Services.Reputaion
{
    public class ReputaionClientCache
    {
        public ClientCache BasicCache { get; init; }
        public MorphClient MorphClient { get; init; }
        public ReputationStorage ReputationStorage { get; init; }

        public ReputationClient Get(string address)
        {
            var client = BasicCache.Get(address);
            try
            {
                var nm = MorphContractInvoker.InvokeSnapshot(MorphClient, 0);
                foreach (var n in nm.Nodes)
                {
                    var ipaddr = IPAddrFromMultiaddr(n.NetworkAddress);
                    if (ipaddr == address)
                    {
                        UpdatePrm prm = new(n.PublicKey);
                        return new()
                        {
                            FSClient = client,
                            Prm = prm,
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Log(nameof(ReputaionClientCache), LogLevel.Debug, e.Message);
            }
            return new()
            {
                FSClient = client,
                Prm = null,
            };
        }
    }
}

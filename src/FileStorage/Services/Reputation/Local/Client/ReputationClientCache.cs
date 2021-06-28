
using System;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Reputaion.Local.Storage;
using static Neo.Utility;
using MorphClient = Neo.FileStorage.Morph.Invoker.Client;

namespace Neo.FileStorage.Services.Reputaion.Local.Client
{
    public class ReputationClientCache : ClientCache
    {
        public StorageService StorageNode { get; init; }
        public MorphClient MorphClient { get; init; }
        public TrustStorage ReputationStorage { get; init; }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override IFSClient Get(Network.Address address)
        {
            var client = base.Get(address);
            try
            {
                var nm = MorphContractInvoker.InvokeSnapshot(MorphClient, 0);
                foreach (var n in nm.Nodes)
                {
                    var ipaddr = Network.Address.FromString(n.NetworkAddress);
                    if (ipaddr.Equals(address))
                    {
                        UpdatePrm prm = new(new(n.PublicKey));
                        return new ReputationClient()
                        {
                            ClientCache = this,
                            FSClient = client,
                            Prm = prm,
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Log(nameof(ReputationClientCache), LogLevel.Debug, e.Message);
            }
            return new ReputationClient()
            {
                FSClient = client,
                Prm = null,
            };
        }
    }
}


using System;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Storage;
using static Neo.Utility;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local.Client
{
    public class ReputationClientCache : ClientCache
    {
        public IEpochSource EpochSource { get; init; }
        public MorphInvoker MorphInvoker { get; init; }
        public TrustStorage ReputationStorage { get; init; }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override IFSClient Get(List<Network.Address> addresses)
        {
            var client = base.Get(addresses);
            try
            {
                var nm = MorphInvoker.GetNetMapByDiff(0);
                foreach (var n in nm.Nodes)
                {
                    var addrs = n.NetworkAddresses.Select(p => Network.Address.FromString(p)).ToList();
                    if (addrs.Intersect(addresses).Any())
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

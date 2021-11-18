using System.Collections.Generic;
using System.Collections.Concurrent;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Network;
using System.Linq;

namespace Neo.FileStorage.Cache
{
    public class ClientCache : IFSClientCache
    {
        private readonly ConcurrentDictionary<string, MultiClient> clients = new();

        public virtual IFSClient Get(IEnumerable<Address> addresses)
        {
            var saddrs = string.Join("\n", addresses.Select(p => p.ToString()));
            if (!clients.TryGetValue(saddrs, out MultiClient mClient))
            {
                mClient = new MultiClient(addresses);
                clients[saddrs] = mClient;
            }
            return mClient;
        }

        public virtual void Dispose()
        {
            foreach (var client in clients.Values)
            {
                client.Dispose();
            }
            clients.Clear();
        }
    }
}

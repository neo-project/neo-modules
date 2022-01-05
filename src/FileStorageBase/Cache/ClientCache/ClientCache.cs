using System.Collections.Concurrent;
using Neo.FileStorage.API.Client;
using System.Linq;
using Neo.FileStorage.API.Netmap;
using static Neo.FileStorage.Network.Helper;

namespace Neo.FileStorage.Cache
{
    public class ClientCache : IFSClientCache
    {
        private readonly ConcurrentDictionary<string, MultiClient> clients = new();

        public virtual IFSClient Get(NodeInfo node)
        {
            var pk = node.PublicKey.ToString();
            if (!clients.TryGetValue(pk, out MultiClient mClient))
            {
                mClient = new MultiClient(node.Addresses.ToList().ToNetworkAddresses());
                clients[pk] = mClient;
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

using System.Collections.Concurrent;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Cache
{
    public class ClientCache : IFSClientCache
    {
        private readonly ConcurrentDictionary<string, MultiClient> clients = new();

        public virtual IFSClient Get(NodeInfo node)
        {
            var pk = node.PublicKey.ToBase64();
            if (!clients.TryGetValue(pk, out MultiClient mClient))
            {
                mClient = new MultiClient(node);
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

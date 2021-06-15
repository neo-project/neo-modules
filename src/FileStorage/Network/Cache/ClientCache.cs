using System;
using System.Collections.Concurrent;
using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.Network.Cache
{
    public class ClientCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, Client> clients = new ConcurrentDictionary<string, Client>();

        public Client Get(Address address)
        {
            var m_addr = address.ToString();
            if (clients.TryGetValue(m_addr, out Client client))
            {
                return client;
            }
            //TODO: tls
            var host = address.ToHostAddressString();
            client = new Client(null, host);//TODO: make sure there is key in options
            clients[m_addr] = client;
            return client;
        }

        public void Dispose()
        {
            foreach (var client in clients.Values)
                client.Dispose();
        }
    }
}

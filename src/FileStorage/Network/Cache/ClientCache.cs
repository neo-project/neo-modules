using Neo.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Neo.FileStorage.Network.Cache
{
    public class ClientCache
    {
        private readonly ConcurrentDictionary<string, Client> clients = new ConcurrentDictionary<string, Client>();

        public Client Get(string address)
        {
            if (clients.TryGetValue(address, out Client client))
            {
                return client;
            }
            client = new Client(null, address);//TODO: make sure there is key in options
            clients[address] = client;
            return client;
        }
    }
}

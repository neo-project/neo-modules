using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.Cryptography;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Neo.FileStorage.Network.Cache
{
    public class ClientCache
    {
        private readonly ConcurrentDictionary<string, Client> clients = new ConcurrentDictionary<string, Client>();

        public Client GetClient(ECDsa key, string address)
        {
            var id = UniqueID(key, address);
            if (clients.TryGetValue(id, out Client client))
            {
                return client;
            }
            client = new Client(key, address);
            clients[id] = client;
            return client;
        }

        private string UniqueID(ECDsa key, string address)
        {
            var finger_print = key.ExportECPrivateKey().Sha256();
            return Base58.Encode(finger_print) + address;
        }
    }
}
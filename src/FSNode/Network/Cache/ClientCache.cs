using NeoFS.API.v2.Client;
using NeoFS.API.v2.Cryptography;
using Neo.Cryptography;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Neo.FSNode.Network.Cache
{
    public class ClientCache
    {
        private readonly ConcurrentDictionary<string, Client> clients = new ConcurrentDictionary<string, Client>();

        public Client GetClient(byte[] private_key, string address)
        {
            var key = private_key.LoadPrivateKey();
            var id = UniqueID(key, address);
            if (clients.TryGetValue(id, out Client client))
            {
                return client;
            }
            client = new Client(address, key);
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
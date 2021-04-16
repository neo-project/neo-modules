using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;
using System;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public partial class ContractInvoker
    {
        public static void InnerRingIndex(Client client, ECPoint key, out int index, out int length)
        {
            if (client is null) throw new Exception("client is nil");
            ECPoint[] innerRing = client.NeoFSAlphabetList();
            index = KeyPosition(key, innerRing);
            length = innerRing.Length;
        }
        public static int AlphabetIndex(Client client, ECPoint key)
        {
            if (client is null) throw new Exception("client is nil");
            return KeyPosition(key, client.Committee());
        }

        private static int KeyPosition(ECPoint key, ECPoint[] list)
        {
            var result = -1;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(key))
                {
                    result = i;
                    break;
                }
            }
            return result;
        }
    }
}

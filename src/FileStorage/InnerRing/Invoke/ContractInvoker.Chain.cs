using Neo.Cryptography.ECC;
using Neo.Plugins.FSStorage.morph.invoke;

namespace Neo.Plugins.FSStorage.innerring.invoke
{
    public partial class ContractInvoker
    {
        public static void InnerRingIndex(Client client, ECPoint key,out int index,out int length)
        {
            ECPoint[] innerRing = client.NeoFSAlphabetList();
            index = KeyPosition(key, innerRing);
            length = innerRing.Length;
        }
        public static int AlphabetIndex(Client client, ECPoint key)
        {
            ECPoint[] alphabet =client.Committee();
            return KeyPosition(key, alphabet);
        }

        private static int KeyPosition(ECPoint key, ECPoint[] list) {
            var result = -1;
            for (int i = 0; i < list.Length; i++) {
                if (list[i].Equals(key)) {
                    result = i;
                    break;
                }
            }
            return result;
        }
    }
}

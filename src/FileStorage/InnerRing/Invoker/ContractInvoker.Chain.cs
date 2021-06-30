using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Linq;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public static partial class ContractInvoker
    {
        public static void InnerRingIndex(this Client client, ECPoint key, out int index, out int length)
        {
            if (client is null) throw new Exception("client is nil");
            ECPoint[] innerRing = client.NeoFSAlphabetList();
            index = KeyPosition(key, innerRing);
            length = innerRing.Length;
        }

        public static int AlphabetIndex(this Client client, ECPoint key)
        {
            if (client is null) throw new Exception("client is nil");
            Console.WriteLine("当前侧链委员会成员：");
            client.Committee().ToList().ForEach(p=>Console.WriteLine(p.ToString()));
            Console.WriteLine("本节点公钥：" + key.ToString());
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
            Console.WriteLine("KeyPosition结果："+ result);
            return result;
        }
    }
}
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.Plugins
{
    public static class TestUtils
    {
        public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, ISerializable key)
        {
            var k = new KeyBuilder(contract.Id, prefix);
            if (key != null) k = k.Add(key);
            return k;
        }

        public static StorageKey CreateStorageKey(this NativeContract contract, byte prefix, uint value)
        {
            return new KeyBuilder(contract.Id, prefix).AddBigEndian(value);
        }
    }
}

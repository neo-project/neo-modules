using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoker;
using System;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public static partial class ContractInvoker
    {
        private static UInt160 ContainerContractHash => Settings.Default.ContainerContractHash;
        private const string PutContainerMethod = "put";
        private const string DeleteContainerMethod = "delete";

        public static bool RegisterContainer(this Client client, ECPoint key, byte[] container, byte[] signature)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, ContainerContractHash, PutContainerMethod, 5 * ExtraFee, container, signature, key.EncodePoint(true));
        }

        public static bool RemoveContainer(this Client client, byte[] containerID, byte[] signature)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, ContainerContractHash, DeleteContainerMethod, ExtraFee, containerID, signature);
        }
    }
}

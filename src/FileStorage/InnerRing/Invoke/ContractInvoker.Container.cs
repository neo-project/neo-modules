using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Invoke;

namespace Neo.FileStorage.InnerRing.Invoke
{
    public partial class ContractInvoker
    {
        private static UInt160 ContainerContractHash => Settings.Default.ContainerContractHash;
        private const string PutContainerMethod = "put";
        private const string DeleteContainerMethod = "delete";
        private const string ListContainersMethod = "list";

        public static bool RegisterContainer(IClient client, ECPoint key, byte[] container, byte[] signature)
        {
            return client.InvokeFunction(ContainerContractHash, PutContainerMethod, 5 * ExtraFee, container, signature, key.EncodePoint(true));
        }

        public static bool RemoveContainer(IClient client, byte[] containerID, byte[] signature)
        {
            return client.InvokeFunction(ContainerContractHash, DeleteContainerMethod, ExtraFee, containerID, signature);
        }
    }
}

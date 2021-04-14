using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Plugins.FSStorage.morph.invoke;
using NeoFS.API.v2.Netmap;
using System.Collections.Generic;

namespace Neo.Plugins.FSStorage.innerring.invoke
{
    public partial class ContractInvoker
    {
        private static UInt160 NetMapContractHash => Settings.Default.NetmapContractHash;
        private const string GetEpochMethod = "epoch";
        private const string SetNewEpochMethod = "newEpoch";
        private const string ApprovePeerMethod = "addPeer";
        private const string UpdatePeerStateMethod = "updateState";
        private const string SetConfigMethod = "setConfigMethod";
        private const string GetNetmapSnapshotMethod = "netmap";

        public static long GetEpoch(Client client)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, GetEpochMethod);
            return (long)(result.ResultStack[0].GetInteger());
        }

        public static bool SetNewEpoch(Client client, ulong epoch)
        {
            return client.Invoke(out _, NetMapContractHash, SetNewEpochMethod, FeeOneGas, epoch);
        }

        public static bool ApprovePeer(Client client, byte[] peer)
        {
            return client.Invoke(out _, NetMapContractHash, ApprovePeerMethod, FeeOneGas, peer);
        }

        public static bool UpdatePeerState(Client client, ECPoint key, int status)
        {
            return client.Invoke(out _, NetMapContractHash, UpdatePeerStateMethod, ExtraFee, status, key.ToArray());
        }

        public static bool SetConfig(Client client, byte[] Id, byte[] key, byte[] value)
        {
            return client.Invoke(out _, NetMapContractHash, SetConfigMethod, ExtraFee, Id, key, value);
        }

        public static NodeInfo[] NetmapSnapshot(Client client)
        {
            InvokeResult invokeResult = client.TestInvoke(NetMapContractHash, GetNetmapSnapshotMethod);
            var rawNodeInfos = ((VM.Types.Array)invokeResult.ResultStack[0]).GetEnumerator();
            var result = new List<NodeInfo>();
            while (rawNodeInfos.MoveNext())
            {
                var item = (VM.Types.Array)rawNodeInfos.Current;
                var rawNodeInfo = item[0].GetSpan().ToArray();
                NodeInfo node = NodeInfo.Parser.ParseFrom(rawNodeInfo);
                result.Add(node);
            }
            return result.ToArray();
        }
    }
}

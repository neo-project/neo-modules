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
        private const string SetConfigMethod = "setConfig";
        private const string UpdateInnerRingMethod = "updateInnerRing";
        private const string GetNetmapSnapshotMethod = "netmap";

        public class UpdatePeerArgs
        {
            public ECPoint Key;
            public int Status;
        }

        public class SetConfigArgs
        {
            public byte[] Id;
            public byte[] Key;
            public byte[] Value;
        }

        public static long GetEpoch(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(NetMapContractHash, GetEpochMethod);
            return (long)(result.ResultStack[0].GetInteger());
        }

        public static bool SetNewEpoch(IClient client, ulong epoch)
        {
            return client.InvokeFunction(NetMapContractHash, SetNewEpochMethod, FeeOneGas, epoch);
        }

        public static bool ApprovePeer(IClient client, byte[] peer)
        {
            return client.InvokeFunction(NetMapContractHash, ApprovePeerMethod, FeeOneGas, peer);
        }

        public static bool UpdatePeerState(IClient client, UpdatePeerArgs p)
        {
            return client.InvokeFunction(NetMapContractHash, UpdatePeerStateMethod, ExtraFee, p.Status, p.Key.ToArray());
        }

        public static bool SetConfig(IClient client, SetConfigArgs p)
        {
            return client.InvokeFunction(NetMapContractHash, SetConfigMethod, ExtraFee, p.Id, p.Key, p.Value);
        }

        public static bool UpdateInnerRing(IClient client, ECPoint[] p)
        {
            List<byte[]> keys = new List<byte[]>();
            foreach (ECPoint e in p)
            {
                keys.Add(e.ToArray());
            }
            return client.InvokeFunction(NetMapContractHash, UpdateInnerRingMethod, ExtraFee, keys.ToArray());
        }

        public static NodeInfo[] NetmapSnapshot(IClient client)
        {
            InvokeResult invokeResult = client.InvokeLocalFunction(NetMapContractHash, GetNetmapSnapshotMethod);
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

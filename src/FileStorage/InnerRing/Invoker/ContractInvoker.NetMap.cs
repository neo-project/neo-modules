using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.FileStorage.API.Netmap;
using System.Collections.Generic;
using Neo.FileStorage.Morph.Invoker;
using System;
using Neo.VM;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public static partial class ContractInvoker
    {
        private static UInt160 NetMapContractHash => Settings.Default.NetmapContractHash;
        private const string GetEpochMethod = "epoch";
        private const string SetNewEpochMethod = "newEpoch";
        private const string ApprovePeerMethod = "addPeer";
        private const string UpdatePeerStateMethod = "updateState";
        private const string SetConfigMethod = "setConfigMethod";
        private const string GetNetmapSnapshotMethod = "netmap";

        public static long GetEpoch(this Client client)
        {
            if (client is null) throw new Exception("client is nil");
            InvokeResult result = client.TestInvoke(NetMapContractHash, GetEpochMethod);
            if (result.State != VMState.HALT) throw new Exception("can't get epoch");
            return (long)(result.ResultStack[0].GetInteger());
        }

        public static bool SetNewEpoch(this Client client, ulong epoch)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, NetMapContractHash, SetNewEpochMethod, FeeOneGas, epoch);
        }

        public static bool ApprovePeer(this Client client, byte[] peer)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, NetMapContractHash, ApprovePeerMethod, FeeOneGas, peer);
        }

        public static bool UpdatePeerState(this Client client, ECPoint key, int status)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, NetMapContractHash, UpdatePeerStateMethod, ExtraFee, status, key.ToArray());
        }

        public static bool SetConfig(this Client client, byte[] Id, byte[] key, byte[] value)
        {
            if (client is null) throw new Exception("client is nil");
            return client.Invoke(out _, NetMapContractHash, SetConfigMethod, ExtraFee, Id, key, value);
        }

        public static NodeInfo[] NetmapSnapshot(this Client client)
        {
            if (client is null) throw new Exception("client is nil");
            InvokeResult invokeResult = client.TestInvoke(NetMapContractHash, GetNetmapSnapshotMethod);
            if (invokeResult.State != VMState.HALT) throw new Exception("can't get netmap snapshot");
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

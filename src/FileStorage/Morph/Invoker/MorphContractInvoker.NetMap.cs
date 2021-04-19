using Neo.FileStorage.API.Netmap;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;
using static Neo.FileStorage.API.Netmap.Helper;

namespace Neo.FileStorage.Morph.Invoker
{
    public partial class MorphContractInvoker
    {
        public static readonly byte[] MaxObjectSizeConfig = Utility.StrictUTF8.GetBytes("MaxObjectSize");
        public static readonly byte[] BasicIncomeRateConfig = Utility.StrictUTF8.GetBytes("BasicIncomeRate");

        private static UInt160 NetMapContractHash => Settings.Default.NetmapContractHash;
        private const string AddPeerMethod = "addPeer";
        private const string NewEpochMethod = "newEpoch";
        private const string UpdateStateMethod = "updateState";
        private const string ConfigMethod = "config";
        private const string EpochMethod = "epoch";
        private const string SnapshotMethod = "snapshot";
        private const string NetMapMethod = "netmap";
        private const string EpochSnapshotMethod = "snapshotByEpoch";
        private const long ExtraFee = 0;

        public static bool InvokeAddPeer(Client client, byte[] info)
        {
            return client.Invoke(out _, NetMapContractHash, AddPeerMethod, ExtraFee, info);
        }

        public static byte[] InvokeConfig(Client client, byte[] key)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, ConfigMethod, key);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Config)");
            return result.ResultStack[0].GetSpan().ToArray();
        }
        public static ulong InvokeEpoch(Client client)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, EpochMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Epoch)");
            return (ulong)result.ResultStack[0].GetInteger();
        }

        public static bool InvokeNewEpoch(Client client, long epochNumber)
        {
            return client.Invoke(out _, NetMapContractHash, NewEpochMethod, ExtraFee, epochNumber);
        }

        public static bool InvokeUpdateState(Client client, long state, byte[] key)
        {
            return client.Invoke(out _, NetMapContractHash, UpdateStateMethod, ExtraFee, state, key);
        }

        public static NetMap InvokeNetMap(Client client)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, NetMapMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (NetMap)");
            if (result.ResultStack.Length != 1) throw new Exception(string.Format("unexpected stack item count ({0})", result.ResultStack.Length));
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new();
            while (peersEnumerator.MoveNext())
            {
                Array peer = (Array)peersEnumerator.Current;
                if (peer.Count != 1) throw new Exception(string.Format("unexpected stack item count (PeerInfo): expected {0}, has {1}", 1, peer.Count));
                IEnumerator<StackItem> peerEnumerator = peer.GetEnumerator();
                while (peerEnumerator.MoveNext())
                {
                    res.Add(peerEnumerator.Current.GetSpan().ToArray());
                }
            }

            return new(res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToList().InfoToNodes());
        }

        public static NetMap InvokeSnapshot(Client client, int different)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, SnapshotMethod, different);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Snapshot)");
            if (result.ResultStack.Length != 1) throw new Exception(string.Format("unexpected stack item count ({0})", result.ResultStack.Length));
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new();
            while (peersEnumerator.MoveNext())
            {
                Array peer = (Array)peersEnumerator.Current;
                if (peer.Count != 1) throw new Exception(string.Format("unexpected stack item count (PeerInfo): expected {0}, has {1}", 1, peer.Count));
                IEnumerator<StackItem> peerEnumerator = peer.GetEnumerator();
                while (peerEnumerator.MoveNext())
                {
                    res.Add(peerEnumerator.Current.GetSpan().ToArray());
                }
            }
            return new(res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToList().InfoToNodes());
        }

        public static NetMap InvokeEpochSnapshot(Client client, long epoch)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, EpochSnapshotMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (EpochSnapshot)");
            if (result.ResultStack.Length != 1) throw new Exception(string.Format("unexpected stack item count ({0})", result.ResultStack.Length));
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new();
            while (peersEnumerator.MoveNext())
            {
                Array peer = (Array)peersEnumerator.Current;
                if (peer.Count != 1) throw new Exception(string.Format("unexpected stack item count (PeerInfo): expected {0}, has {1}", 1, peer.Count));
                IEnumerator<StackItem> peerEnumerator = peer.GetEnumerator();
                while (peerEnumerator.MoveNext())
                {
                    res.Add(peerEnumerator.Current.GetSpan().ToArray());
                }
            }
            return new(res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToList().InfoToNodes());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.VM.Types;
using static Neo.FileStorage.API.Netmap.Helper;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.Morph.Invoker
{
    public static partial class MorphContractInvoker
    {
        private static UInt160 NetMapContractHash => Settings.Default.NetmapContractHash;
        private const string AddPeerMethod = "addPeer";
        private const string NewEpochMethod = "newEpoch";
        private const string UpdateStateMethod = "updateState";
        private const string EpochMethod = "epoch";
        private const string SnapshotMethod = "snapshot";
        private const string NetMapMethod = "netmap";
        private const string EpochSnapshotMethod = "snapshotByEpoch";
        private const long ExtraFee = 0;

        public static bool InvokeAddPeer(this Client client, NodeInfo info)
        {
            return client.Invoke(out _, NetMapContractHash, AddPeerMethod, SideChainFee, info.ToByteArray());
        }

        public static ulong InvokeEpoch(this Client client)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, EpochMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Epoch)");
            return (ulong)result.ResultStack[0].GetInteger();
        }

        public static bool InvokeNewEpoch(this Client client, long epochNumber)
        {
            return client.Invoke(out _, NetMapContractHash, NewEpochMethod, SideChainFee, epochNumber);
        }

        public static bool InvokeUpdateState(this Client client, long state, byte[] key)
        {
            return client.Invoke(out _, NetMapContractHash, UpdateStateMethod, SideChainFee, state, key);
        }

        public static NetMap InvokeNetMap(this Client client)
        {
            InvokeResult result = client.TestInvoke(NetMapContractHash, NetMapMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (NetMap)");
            if (result.ResultStack.Length != 1) throw new Exception(string.Format("unexpected stack item count ({0})", result.ResultStack.Length));
            if (result.ResultStack[0] is VM.Types.Null) return new NetMap(new List<Node>());
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

        public static NetMap InvokeSnapshot(this Client client, int different)
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

        public static NetMap InvokeEpochSnapshot(this Client client, ulong epoch)
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

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Netmap;
using Neo.IO;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using static Neo.FileStorage.API.Netmap.Helper;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private const string AddPeerMethod = "addPeer";
        private const string NewEpochMethod = "newEpoch";
        private const string UpdateStateMethod = "updateState";
        private const string EpochMethod = "epoch";
        private const string SnapshotMethod = "snapshot";
        private const string NetMapMethod = "netmap";
        private const string EpochSnapshotMethod = "snapshotByEpoch";
        private const string SetInnerRingMethod = "updateInnerRing";

        private const long ExtraFee = 0;

        public void AddPeer(NodeInfo info)
        {
            Invoke(NetMapContractHash, AddPeerMethod, SideChainFee, info.ToByteArray());
        }

        public ulong Epoch()
        {
            InvokeResult result = TestInvoke(NetMapContractHash, EpochMethod);
            return (ulong)result.ResultStack[0].GetInteger();
        }

        public void NewEpoch(ulong epochNumber)
        {
            Invoke(NetMapContractHash, NewEpochMethod, SideChainFee, epochNumber);
        }

        public void UpdatePeerState(NodeInfo.Types.State state, byte[] key)
        {
            Invoke(NetMapContractHash, UpdateStateMethod, SideChainFee, (int)state, key);
        }

        public NodeInfo[] NetMap()
        {
            InvokeResult result = TestInvoke(NetMapContractHash, NetMapMethod);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            if (result.ResultStack[0] is VM.Types.Null) return System.Array.Empty<NodeInfo>();
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new();
            while (peersEnumerator.MoveNext())
            {
                Array peer = (Array)peersEnumerator.Current;
                if (peer.Count != 1) throw new Exception($"unexpected stack item count peer info, expected={1}, actual={peer.Count}");
                IEnumerator<StackItem> peerEnumerator = peer.GetEnumerator();
                while (peerEnumerator.MoveNext())
                {
                    res.Add(peerEnumerator.Current.GetSpan().ToArray());
                }
            }

            return res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToArray();
        }

        public NetMap GetNetMapByDiff(int different)
        {
            InvokeResult result = TestInvoke(NetMapContractHash, SnapshotMethod, different);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new();
            while (peersEnumerator.MoveNext())
            {
                Array peer = (Array)peersEnumerator.Current;
                if (peer.Count != 1) throw new InvalidOperationException($"unexpected stack item count peer info, expected={1}, actual={peer.Count}");
                IEnumerator<StackItem> peerEnumerator = peer.GetEnumerator();
                while (peerEnumerator.MoveNext())
                {
                    res.Add(peerEnumerator.Current.GetSpan().ToArray());
                }
            }
            return new(res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToList().InfoToNodes());
        }

        public NetMap GetNetMapByEpoch(ulong epoch)
        {
            InvokeResult result = TestInvoke(NetMapContractHash, EpochSnapshotMethod, epoch);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new();
            while (peersEnumerator.MoveNext())
            {
                Array peer = (Array)peersEnumerator.Current;
                if (peer.Count != 1) throw new InvalidOperationException($"unexpected stack item count peer info, expected={1}, actual={peer.Count}");
                IEnumerator<StackItem> peerEnumerator = peer.GetEnumerator();
                while (peerEnumerator.MoveNext())
                {
                    res.Add(peerEnumerator.Current.GetSpan().ToArray());
                }
            }
            return new(res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToList().InfoToNodes());
        }

        public void ApprovePeer(byte[] peer)
        {
            Invoke(NetMapContractHash, AddPeerMethod, SideChainFee, peer);
        }

        public void SetInnerRing(ECPoint[] publicKeys)
        {
            var array = new ContractParameter(ContractParameterType.Array);
            var list = new List<ContractParameter>();
            foreach (var publicKey in publicKeys)
            {
                list.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = publicKey });
            }
            array.Value = list;
            Invoke(NetMapContractHash, SetInnerRingMethod, SideChainFee, array);
        }
    }
}

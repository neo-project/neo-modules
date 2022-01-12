using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Netmap;
using Neo.IO;
using Neo.SmartContract;
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
        private const string InnerRingListMethod = "innerRingList";

        public void AddPeer(NodeInfo info)
        {
            Invoke(NetMapContractHash, AddPeerMethod, SideChainFee, info.ToByteArray());
        }

        public ulong Epoch()
        {
            var result = TestInvoke(NetMapContractHash, EpochMethod);
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
            var result = TestInvoke(NetMapContractHash, NetMapMethod);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            if (result.ResultStack[0] is VM.Types.Null) return System.Array.Empty<NodeInfo>();
            var nss = (Array)result.ResultStack[0];
            List<byte[]> res = new();
            foreach (Array ns in nss)
            {
                if (ns.Count != 1) throw new InvalidOperationException($"unexpected stack item count peer info, expected={1}, actual={ns.Count}");
                foreach (var n in ns)
                {
                    res.Add(n.GetSpan().ToArray());
                }
            }

            return res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToArray();
        }

        public NetMap GetNetMapByDiff(ulong different)
        {
            var result = TestInvoke(NetMapContractHash, SnapshotMethod, different);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            var nss = (Array)result.ResultStack[0];
            List<byte[]> res = new();
            foreach (Array ns in nss)
            {
                if (ns.Count != 1) throw new InvalidOperationException($"unexpected stack item count peer info, expected={1}, actual={ns.Count}");
                foreach (var n in ns)
                {
                    res.Add(n.GetSpan().ToArray());
                }
            }
            return new(res.Select(p => NodeInfo.Parser.ParseFrom(p)).ToList().InfoToNodes());
        }

        public NetMap GetNetMapByEpoch(ulong epoch)
        {
            var result = TestInvoke(NetMapContractHash, EpochSnapshotMethod, epoch);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            var nss = (Array)result.ResultStack[0];
            List<byte[]> res = new();
            foreach (Array ns in nss)
            {
                if (ns.Count != 1) throw new InvalidOperationException($"unexpected stack item count peer info, expected={1}, actual={ns.Count}");
                foreach (var n in ns)
                {
                    res.Add(n.GetSpan().ToArray());
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

        public List<ECPoint> InnerRingList()
        {
            var result = TestInvoke(NetMapContractHash, InnerRingListMethod);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}, expected={1}");
            var irNodes = (Array)result.ResultStack[0];
            List<ECPoint> irs = new();
            foreach (var n in irNodes)
            {
                var m = (Array)n;
                foreach (var val in m)
                {
                    irs.Add(ECPoint.DecodePoint(val.GetSpan().ToArray(), ECCurve.Secp256r1));
                }
            }
            return irs;
        }
    }
}

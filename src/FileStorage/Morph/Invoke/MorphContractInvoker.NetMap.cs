using Neo.VM.Types;
using System;
using System.Collections.Generic;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.Morph.Invoke
{
    public partial class MorphContractInvoker
    {
        private static UInt160 NetMapContractHash => Settings.Default.NetmapContractHash;
        private const string AddPeerMethod = "addPeer";
        private const string NewEpochMethod = "newEpoch";
        private const string InnerRingListMethod = "innerRingList";
        private const string UpdateStateMethod = "updateState";
        private const string ConfigMethod = "config";
        private const string EpochMethod = "epoch";
        private const string SnapshotMethod = "snapshot";
        private const string NetMapMethod = "netmap";
        private const long ExtraFee = 0;

        public class UpdateStateArgs
        {
            public byte[] key;
            public long state;
        }

        public static bool InvokeAddPeer(IClient client, byte[] info)
        {
            return client.InvokeFunction(NetMapContractHash, AddPeerMethod, ExtraFee, info);
        }

        public static byte[] InvokeConfig(IClient client, byte[] key)
        {
            InvokeResult result = client.InvokeLocalFunction(NetMapContractHash, ConfigMethod, key);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Config)");
            return result.ResultStack[0].GetSpan().ToArray();
        }
        public static long InvokeEpoch(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(NetMapContractHash, EpochMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Epoch)");
            return (long)result.ResultStack[0].GetInteger();
        }

        public static bool InvokeNewEpoch(IClient client, long epochNumber)
        {
            return client.InvokeFunction(NetMapContractHash, NewEpochMethod, ExtraFee, epochNumber);
        }

        public static byte[][] InvokeInnerRingList(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(NetMapContractHash, InnerRingListMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (InnerRingList)");
            Array array = (Array)result.ResultStack[0];
            IEnumerator<StackItem> enumerator = array.GetEnumerator();
            List<byte[]> resultArray = new List<byte[]>();
            while (enumerator.MoveNext())
            {
                resultArray.Add(((Array)enumerator.Current)[0].GetSpan().ToArray());
            }
            return resultArray.ToArray();
        }

        public static bool InvokeUpdateState(IClient client, UpdateStateArgs args)
        {
            return client.InvokeFunction(NetMapContractHash, UpdateStateMethod, ExtraFee, args.state, args.key);
        }

        public static byte[][] InvokeNetMap(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(NetMapContractHash, NetMapMethod);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (NetMap)");
            if (result.ResultStack.Length != 1) throw new Exception(string.Format("unexpected stack item count ({0})", result.ResultStack.Length));
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new List<byte[]>();
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
            return res.ToArray();
        }

        public static byte[][] InvokeSnapshot(IClient client, int different)
        {
            InvokeResult result = client.InvokeLocalFunction(NetMapContractHash, SnapshotMethod, different);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Snapshot)");
            if (result.ResultStack.Length != 1) throw new Exception(string.Format("unexpected stack item count ({0})", result.ResultStack.Length));
            Array peers = (Array)result.ResultStack[0];
            IEnumerator<StackItem> peersEnumerator = peers.GetEnumerator();
            List<byte[]> res = new List<byte[]>();
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
            return res.ToArray();
        }
    }
}

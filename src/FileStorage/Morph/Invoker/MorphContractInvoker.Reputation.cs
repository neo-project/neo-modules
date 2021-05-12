using Neo.FileStorage.API.Reputation;
using Neo.VM.Types;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Morph.Invoker
{
    public static partial class MorphContractInvoker
    {
        private static UInt160 ReputationContractHash => Settings.Default.ReputationContractHash;
        private const string ReputationPutMethod = "put";
        private const string ReputationGetMethod = "get";
        private const string ReputationGetByIDMethod = "getByID";
        private const string ReputationListByEpochMethod = "listByEpoch";

        public static bool InvokeReputationPut(this Client client, long epoch, byte[] peerID, byte[] value)
        {
            return client.Invoke(out _,ReputationContractHash, ReputationPutMethod, epoch, peerID, value);
        }

        public static List<GlobalTrust> InvokeReputationGet(this Client client, long epoch, byte[] peerID)
        {
            InvokeResult result = client.TestInvoke(ReputationContractHash, ReputationGetMethod, epoch, peerID);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ReputationGetMethod));
            if (result.ResultStack.Length != 1) throw new Exception();
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            IEnumerator<StackItem> itemsEnumerator = items.GetEnumerator();
            List<GlobalTrust> lists = new();
            while (itemsEnumerator.MoveNext())
            {
                lists.Add(GlobalTrust.Parser.ParseFrom(itemsEnumerator.Current.GetSpan().ToArray()));
            }
            return lists;
        }

        public static List<GlobalTrust> InvokeReputationGetByID(this Client client, byte[] id)
        {
            InvokeResult result = client.TestInvoke(ReputationContractHash, ReputationGetByIDMethod, id);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ReputationGetByIDMethod));
            if (result.ResultStack.Length != 1) throw new Exception();
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            IEnumerator<StackItem> itemsEnumerator = items.GetEnumerator();
            List<GlobalTrust> lists = new();
            while (itemsEnumerator.MoveNext())
            {
                lists.Add(GlobalTrust.Parser.ParseFrom(itemsEnumerator.Current.GetSpan().ToArray()));
            }
            return lists;
        }

        public static List<byte[]> InvokeReputationListByEpoch(this Client client, long epoch)
        {
            InvokeResult result = client.TestInvoke(ReputationContractHash, ReputationListByEpochMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ReputationListByEpochMethod));
            if (result.ResultStack.Length != 1) throw new Exception();
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            IEnumerator<StackItem> itemsEnumerator = items.GetEnumerator();
            List<byte[]> ids = new();
            while (itemsEnumerator.MoveNext())
            {
                ids.Add(itemsEnumerator.Current.GetSpan().ToArray());
            }
            return ids;
        }
    }
}

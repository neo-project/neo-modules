using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Reputation;
using Neo.VM.Types;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private const string ReputationPutMethod = "put";
        private const string ReputationGetMethod = "get";
        private const string ReputationGetByIDMethod = "getByID";
        private const string ReputationListByEpochMethod = "listByEpoch";

        public void PutReputation(ulong epoch, byte[] peerID, byte[] value)
        {
            Invoke(ReputationContractHash, ReputationPutMethod, SideChainFee, epoch, peerID, value);
        }

        public List<GlobalTrust> GetReputation(ulong epoch, byte[] peerID)
        {
            InvokeResult result = TestInvoke(ReputationContractHash, ReputationGetMethod, 0, epoch, peerID);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            List<GlobalTrust> lists = new();
            foreach (StackItem current in items)
            {
                lists.Add(GlobalTrust.Parser.ParseFrom(current.GetSpan().ToArray()));
            }
            return lists;
        }

        public List<GlobalTrust> GetReputationByID(byte[] id)
        {
            InvokeResult result = TestInvoke(ReputationContractHash, ReputationGetByIDMethod, id);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            List<GlobalTrust> lists = new();
            foreach (StackItem current in items)
            {
                lists.Add(GlobalTrust.Parser.ParseFrom(current.GetSpan().ToArray()));
            }
            return lists;
        }

        public List<byte[]> ListReputationByEpoch(long epoch)
        {
            InvokeResult result = TestInvoke(ReputationContractHash, ReputationListByEpochMethod, epoch);
            if (result.ResultStack.Length != 1) throw new InvalidOperationException($"unexpected stack item, count={result.ResultStack.Length}");
            VM.Types.Array items = (VM.Types.Array)result.ResultStack[0];
            List<byte[]> ids = new();
            foreach (StackItem current in items)
            {
                ids.Add(current.GetSpan().ToArray());
            }
            return ids;
        }
    }
}

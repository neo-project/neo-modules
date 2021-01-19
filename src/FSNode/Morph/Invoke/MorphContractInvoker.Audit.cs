using Neo.VM.Types;
using System;
using System.Collections.Generic;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.FSStorage.morph.invoke
{
    public partial class MorphContractInvoker
    {
        private static UInt160 AuditContractHash => Settings.Default.BalanceContractHash;
        private const string PutResultMethod = "put";
        private const string GetResultMethod = "get";
        private const string ListResultsMethod = "list";
        private const string ListByEpochResultsMethod = "listByEpoch";
        private const string ListByCIDResultsMethod = "listByCID";
        private const string ListByNodeResultsMethod = "listByNode";

        public static bool InvokePutAuditResult(IClient client, byte[] rawResult)
        {
            return client.InvokeFunction(AuditContractHash, PutResultMethod, 0, rawResult);
        }

        public static byte[] InvokeGetAuditResult(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(AuditContractHash, GetResultMethod);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", GetResultMethod));
            return result.ResultStack[0].GetSpan().ToArray();
        }

        public static byte[][] InvokeListAuditResults(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(AuditContractHash, ListResultsMethod);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static byte[][] InvokeListAuditResultsByEpoch(IClient client,long epoch)
        {
            InvokeResult result = client.InvokeLocalFunction(AuditContractHash, ListByEpochResultsMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListByEpochResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static byte[][] InvokeListAuditResultsByCID(IClient client, long epoch,byte[] cid)
        {
            InvokeResult result = client.InvokeLocalFunction(AuditContractHash, ListByCIDResultsMethod, epoch, cid);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListByEpochResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static byte[][] InvokeListAuditResultsByNode(IClient client, long epoch,byte[] cid,byte[] nodeKey)
        {
            InvokeResult result = client.InvokeLocalFunction(AuditContractHash, ListByNodeResultsMethod, epoch,cid, nodeKey);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListByEpochResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static byte[][] ParseAuditResults(StackItem result) {
            Array array = (Array)result;
            IEnumerator<StackItem> enumerator = array.GetEnumerator();
            List<byte[]> resultArray = new List<byte[]>();
            while (enumerator.MoveNext())
            {
                resultArray.Add(enumerator.Current.GetSpan().ToArray());
            }
            return resultArray.ToArray();
        }
    }
}

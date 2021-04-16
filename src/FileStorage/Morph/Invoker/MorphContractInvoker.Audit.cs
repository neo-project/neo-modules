using Neo.VM.Types;
using Neo.FileStorage.API.Audit;
using System;
using System.Collections.Generic;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.Morph.Invoker
{
    public partial class MorphContractInvoker
    {
        private static UInt160 AuditContractHash => Settings.Default.AuditContractHash;
        private const string PutResultMethod = "put";
        private const string GetResultMethod = "get";
        private const string ListResultsMethod = "list";
        private const string ListByEpochResultsMethod = "listByEpoch";
        private const string ListByCIDResultsMethod = "listByCID";
        private const string ListByNodeResultsMethod = "listByNode";

        public static bool InvokePutAuditResult(Client client, byte[] rawResult)
        {
            return client.Invoke(out _, AuditContractHash, PutResultMethod, 0, rawResult);
        }

        public static DataAuditResult InvokeGetAuditResult(Client client)
        {
            InvokeResult result = client.TestInvoke(AuditContractHash, GetResultMethod);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", GetResultMethod));
            return DataAuditResult.Parser.ParseFrom(result.ResultStack[0].GetSpan().ToArray());
        }

        public static List<byte[]> InvokeListAuditResults(Client client)
        {
            InvokeResult result = client.TestInvoke(AuditContractHash, ListResultsMethod);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static List<byte[]> InvokeListAuditResultsByEpoch(Client client, long epoch)
        {
            InvokeResult result = client.TestInvoke(AuditContractHash, ListByEpochResultsMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListByEpochResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static List<byte[]> InvokeListAuditResultsByCID(Client client, long epoch, byte[] cid)
        {
            InvokeResult result = client.TestInvoke(AuditContractHash, ListByCIDResultsMethod, epoch, cid);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListByEpochResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static List<byte[]> InvokeListAuditResultsByNode(Client client, long epoch, byte[] cid, byte[] nodeKey)
        {
            InvokeResult result = client.TestInvoke(AuditContractHash, ListByNodeResultsMethod, epoch, cid, nodeKey);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", ListByEpochResultsMethod));
            return ParseAuditResults(result.ResultStack[0]);
        }

        public static List<byte[]> ParseAuditResults(StackItem result)
        {
            Array array = (Array)result;
            IEnumerator<StackItem> enumerator = array.GetEnumerator();
            List<byte[]> resultArray = new();
            while (enumerator.MoveNext())
            {
                resultArray.Add(enumerator.Current.GetSpan().ToArray());
            }
            return resultArray;
        }
    }
}

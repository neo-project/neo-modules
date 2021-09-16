using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Audit;
using Neo.VM.Types;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
        private const string PutResultMethod = "put";
        private const string GetResultMethod = "get";
        private const string ListResultsMethod = "list";
        private const string ListByEpochResultsMethod = "listByEpoch";
        private const string ListByCIDResultsMethod = "listByCID";
        private const string ListByNodeResultsMethod = "listByNode";

        public void PutAuditResult(byte[] rawResult)
        {
            Invoke(AuditContractHash, PutResultMethod, SideChainFee, rawResult);
        }

        public DataAuditResult GetAuditResult(byte[] id)
        {
            InvokeResult result = TestInvoke(AuditContractHash, GetResultMethod, id);
            return DataAuditResult.Parser.ParseFrom(result.ResultStack[0].GetSpan().ToArray());
        }

        public List<byte[]> ListAuditResults()
        {
            InvokeResult result = TestInvoke(AuditContractHash, ListResultsMethod);
            return ParseAuditResults(result.ResultStack[0]);
        }

        public List<byte[]> ListAuditResultsByEpoch(long epoch)
        {
            InvokeResult result = TestInvoke(AuditContractHash, ListByEpochResultsMethod, epoch);
            return ParseAuditResults(result.ResultStack[0]);
        }

        public List<byte[]> ListAuditResultsByCID(long epoch, byte[] cid)
        {
            InvokeResult result = TestInvoke(AuditContractHash, ListByCIDResultsMethod, epoch, cid);
            return ParseAuditResults(result.ResultStack[0]);
        }

        public List<byte[]> ListAuditResultsByNode(long epoch, byte[] cid, byte[] nodeKey)
        {
            InvokeResult result = TestInvoke(AuditContractHash, ListByNodeResultsMethod, epoch, cid, nodeKey);
            return ParseAuditResults(result.ResultStack[0]);
        }

        public List<byte[]> ParseAuditResults(StackItem result)
        {
            if (result is Null) return new List<byte[]>();
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

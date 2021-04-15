using Neo.VM.Types;
using System;
using System.Collections.Generic;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.Morph.Invoker
{
    public partial class MorphContractInvoker
    {
        private static UInt160 ContainerContractHash => Settings.Default.ContainerContractHash;
        private const string PutMethod = "put";
        private const string DeleteMethod = "delete";
        private const string GetMethod = "get";
        private const string ListMethod = "list";
        private const string EACLMethod = "eACL";
        private const string SetEACLMethod = "setEACL";
        private const string PutSizeMethod = "putContainerSize";
        private const string ListSizesMethod = "listContainerSizes";
        private const string GetSizeMethod = "getContainerSize";
        private const string StartEstimationMethod = "startContainerEstimation";
        private const string StopEstimationMethod = "stopContainerEstimation";

        public class EACLValues
        {
            public byte[] eacl;
            public byte[] sig;
        }

        public class Estimation
        {
            public long size;
            public byte[] reporter;
        }

        public class Estimations
        {
            public byte[] containerID;
            public Estimation[] estimations;
        }

        public static bool InvokePut(Client client, byte[] cnr, byte[] sig, byte[] publicKey)
        {
            return client.Invoke(out _, ContainerContractHash, PutMethod, ExtraFee, cnr, sig, publicKey);
        }

        public static bool InvokeSetEACL(Client client, byte[] eacl, byte[] sig)
        {
            return client.Invoke(out _, ContainerContractHash, SetEACLMethod, ExtraFee, eacl, sig);
        }

        public static bool InvokeDelete(Client client, byte[] cid, byte[] sig)
        {
            return client.Invoke(out _, ContainerContractHash, DeleteMethod, ExtraFee, cid, sig);
        }
        public static EACLValues InvokeGetEACL(Client client, byte[] containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, EACLMethod, containerID);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (EACL)");
            Array array = (Array)result.ResultStack[0];
            var eacl = array[0].GetSpan().ToArray();
            var sig = array[1].GetSpan().ToArray();
            EACLValues eACLValues = new EACLValues()
            {
                eacl = eacl,
                sig = sig
            };
            return eACLValues;
        }

        public static byte[] InvokeGetContainer(Client client, byte[] containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, GetMethod, containerID);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Get)");
            return result.ResultStack[0].GetSpan().ToArray();
        }

        public static byte[][] InvokeGetContainerList(Client client, byte[] ownerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, ListMethod, ownerID);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (List)");
            Array array = (Array)result.ResultStack[0];
            IEnumerator<StackItem> enumerator = array.GetEnumerator();
            List<byte[]> resultArray = new List<byte[]>();
            while (enumerator.MoveNext())
            {
                resultArray.Add(enumerator.Current.GetSpan().ToArray());
            }
            return resultArray.ToArray();
        }

        public static bool InvokePutSize(Client client, long epoch, byte[] cid, long size, byte[] reporterKey)
        {
            return client.Invoke(out _, ContainerContractHash, PutSizeMethod, epoch, cid, size, reporterKey);
        }

        public static Estimations InvokeGetContainerSize(Client client, byte[] containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, GetSizeMethod, containerID);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (GetContainerSize)");
            Array prms = (Array)result.ResultStack[0];
            var es = new Estimations();
            es.containerID = prms[0].GetSpan().ToArray();
            List<Estimation> estimations = new List<Estimation>();
            prms = (Array)prms[1];
            foreach (var item in prms)
            {
                Array array = (Array)item;
                var e = new Estimation();
                e.reporter = array[0].GetSpan().ToArray();
                e.size = (long)array[1].GetInteger();
                estimations.Add(e);
            }
            es.estimations = estimations.ToArray();
            return es;
        }

        public static byte[][] InvokeListSizes(Client client, long epoch)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, ListSizesMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (ListSizes)");
            Array prms = (Array)result.ResultStack[0];
            List<byte[]> ids = new List<byte[]>();
            foreach (var item in prms)
            {
                var id = item.GetSpan().ToArray();
                ids.Add(id);
            }
            return ids.ToArray();
        }

        public static bool InvokeStartEstimation(Client client, long epoch)
        {
            return client.Invoke(out _, ContainerContractHash, StartEstimationMethod, epoch);
        }

        public static bool InvokeStopEstimation(Client client, long epoch)
        {
            return client.Invoke(out _, ContainerContractHash, StopEstimationMethod, epoch);
        }
    }
}

using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Refs;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;
using FSContainer = Neo.FileStorage.API.Container.Container;
using GByteString = Google.Protobuf.ByteString;

namespace Neo.FileStorage.Morph.Invoker
{
    public static partial class MorphContractInvoker
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

        public class Estimation
        {
            public ulong Size;
            public byte[] Reporter;
        }

        public class Estimations
        {
            public ContainerID ContainerID;
            public List<Estimation> AllEstimation;
        }

        public static bool InvokePut(this Client client, FSContainer cnr, byte[] sign, byte[] pubkey)
        {
            return client.Invoke(out _, ContainerContractHash, PutMethod, ExtraFee, cnr.ToByteArray(), sign, pubkey);
        }

        public static bool InvokeSetEACL(this Client client, EACLTable eacl, byte[] sign)
        {
            return client.Invoke(out _, ContainerContractHash, SetEACLMethod, ExtraFee, eacl.ToByteArray(), sign);
        }

        public static bool InvokeDelete(this Client client, ContainerID cid, byte[] sig)
        {
            return client.Invoke(out _, ContainerContractHash, DeleteMethod, ExtraFee, cid.Value.ToByteArray(), sig);
        }
        public static EAclWithSignature InvokeGetEACL(this Client client, ContainerID containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, EACLMethod, containerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (EACL)");
            Array array = (Array)result.ResultStack[0];
            var eacl = array[0].GetSpan().ToArray();
            var sig = array[1].GetSpan().ToArray();
            var pubkey = array[2].GetSpan().ToArray();
            return new()
            {
                Table = EACLTable.Parser.ParseFrom(eacl),
                Signature = new()
                {
                    Key = GByteString.CopyFrom(pubkey),
                    Sign = GByteString.CopyFrom(sig),
                }
            };
        }

        public static FSContainer InvokeGetContainer(this Client client, ContainerID containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, GetMethod, containerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Get)");
            return FSContainer.Parser.ParseFrom(result.ResultStack[0].GetSpan().ToArray());
        }

        public static List<ContainerID> InvokeGetContainerList(this Client client, OwnerID ownerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, ListMethod, ownerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (List)");
            Array array = (Array)result.ResultStack[0];
            IEnumerator<StackItem> enumerator = array.GetEnumerator();
            List<byte[]> resultArray = new();
            while (enumerator.MoveNext())
            {
                resultArray.Add(enumerator.Current.GetSpan().ToArray());
            }
            return resultArray.Select(p => ContainerID.FromSha256Bytes(p)).ToList();
        }

        public static bool InvokePutSize(this Client client, ulong epoch, ContainerID cid, ulong size, byte[] reporterKey)
        {
            return client.Invoke(out _, ContainerContractHash, PutSizeMethod, ExtraFee, epoch, cid.Value.ToByteArray(), size, reporterKey);
        }

        public static Estimations InvokeGetContainerSize(this Client client, ContainerID containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, GetSizeMethod, containerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (GetContainerSize)");
            Array prms = (Array)result.ResultStack[0];
            Estimations es = new();
            es.ContainerID = ContainerID.FromSha256Bytes(prms[0].GetSpan().ToArray());
            List<Estimation> estimations = new();
            prms = (Array)prms[1];
            foreach (var item in prms)
            {
                Array array = (Array)item;
                Estimation e = new();
                e.Reporter = array[0].GetSpan().ToArray();
                e.Size = (ulong)array[1].GetInteger();
                estimations.Add(e);
            }
            es.AllEstimation = estimations;
            return es;
        }

        public static List<byte[]> InvokeListSizes(this Client client, ulong epoch)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, ListSizesMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (ListSizes)");
            Array prms = (Array)result.ResultStack[0];
            List<byte[]> ids = new();
            foreach (var item in prms)
            {
                var id = item.GetSpan().ToArray();
                ids.Add(id);
            }
            return ids;
        }

        public static bool InvokeStartEstimation(this Client client, long epoch)
        {
            return client.Invoke(out _, ContainerContractHash, StartEstimationMethod, epoch);
        }

        public static bool InvokeStopEstimation(this Client client, long epoch)
        {
            return client.Invoke(out _, ContainerContractHash, StopEstimationMethod, epoch);
        }
    }
}

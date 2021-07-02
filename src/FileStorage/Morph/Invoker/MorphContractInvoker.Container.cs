using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Services.Container.Announcement.Control;
using Neo.VM.Types;
using static Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types;
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

        public static bool PutContainer(this Client client, FSContainer cnr, Signature sig, SessionToken token)
        {
            if (client is null) throw new ArgumentNullException(nameof(cnr));
            return client.Invoke(out _, ContainerContractHash, PutMethod, SideChainFee, cnr.ToByteArray(), sig.Sign.ToByteArray(), sig.Key.ToByteArray(), token.ToByteArray());
        }

        public static bool SetEACL(this Client client, EACLTable eacl, Signature sig, SessionToken token)
        {
            if (client is null) throw new ArgumentNullException(nameof(eacl));
            return client.Invoke(out _, ContainerContractHash, SetEACLMethod, SideChainFee, eacl.ToByteArray(), sig.Key.ToByteArray(), sig.Sign.ToByteArray(), token.ToByteArray());
        }

        public static bool DeleteContainer(this Client client, ContainerID cid, byte[] sig, SessionToken token)
        {
            return client.Invoke(out _, ContainerContractHash, DeleteMethod, SideChainFee, cid.Value.ToByteArray(), sig, token.ToByteArray());
        }

        public static EAclWithSignature GetEACL(this Client client, ContainerID containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, EACLMethod, containerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (EACL)");
            Array array = (Array)result.ResultStack[0];
            if (array.Count != 4) throw new InvalidOperationException($"unexpected eacl stack item count {EACLMethod}: {array.Count}");
            return new()
            {
                Table = EACLTable.Parser.ParseFrom(array[0].GetSpan().ToArray()),
                Signature = new()
                {
                    Key = GByteString.CopyFrom(array[2].GetSpan().ToArray()),
                    Sign = GByteString.CopyFrom(array[1].GetSpan().ToArray()),
                },
                SessionToken = array[3] is VM.Types.Null ? null : SessionToken.Parser.ParseFrom(array[3].GetSpan().ToArray())
            };
        }

        public static ContainerWithSignature GetContainer(this Client client, ContainerID containerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, GetMethod, containerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (Get)");
            Array array = (Array)result.ResultStack[0];
            if (array.Count != 4) throw new InvalidOperationException($"unexpected container stack item count: {array.Count}");
            ContainerWithSignature cnr = new()
            {
                Container = FSContainer.Parser.ParseFrom(array[0].GetSpan().ToArray()),
                Signature = new()
                {
                    Sign = GByteString.CopyFrom(array[1].GetSpan().ToArray()),
                    Key = GByteString.CopyFrom(array[2].GetSpan().ToArray()),
                },
                SessionToken = array[3] is VM.Types.Null ? null : SessionToken.Parser.ParseFrom(array[3].GetSpan().ToArray())
            };
            return cnr;
        }

        public static List<ContainerID> ListContainers(this Client client, OwnerID ownerID)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, ListMethod, ownerID.Value.ToByteArray());
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (List)");
            if (result.ResultStack[0] is Null) return new List<ContainerID>();
            Array array = (Array)result.ResultStack[0];
            IEnumerator<StackItem> enumerator = array.GetEnumerator();
            List<byte[]> resultArray = new();
            while (enumerator.MoveNext())
            {
                resultArray.Add(enumerator.Current.GetSpan().ToArray());
            }
            return resultArray.Select(p => ContainerID.FromSha256Bytes(p)).ToList();
        }

        public static bool AnnounceLoad(this Client client, Announcement announcement, byte[] key)
        {
            return client.Invoke(out _, ContainerContractHash, PutSizeMethod, SideChainFee, announcement.Epoch, announcement.ContainerId.Value.ToByteArray(), announcement.UsedSpace, key);
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

        public static List<byte[]> ListSizes(this Client client, ulong epoch)
        {
            InvokeResult result = client.TestInvoke(ContainerContractHash, ListSizesMethod, epoch);
            if (result.State != VM.VMState.HALT) throw new Exception("could not invoke method (ListSizes)");
            if (result.ResultStack[0] is Null) return new List<byte[]>();
            Array prms = (Array)result.ResultStack[0];
            List<byte[]> ids = new();
            foreach (var item in prms)
            {
                var id = item.GetSpan().ToArray();
                ids.Add(id);
            }
            return ids;
        }

        public static bool StartEstimation(this Client client, long epoch)
        {
            return client.Invoke(out _, ContainerContractHash, StartEstimationMethod, SideChainFee, epoch);
        }

        public static bool StopEstimation(this Client client, long epoch)
        {
            return client.Invoke(out _, ContainerContractHash, StopEstimationMethod, SideChainFee, epoch);
        }
    }
}

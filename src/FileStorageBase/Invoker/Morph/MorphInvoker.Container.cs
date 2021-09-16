using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.VM.Types;
using static Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types;
using Array = Neo.VM.Types.Array;
using FSContainer = Neo.FileStorage.API.Container.Container;
using GByteString = Google.Protobuf.ByteString;

namespace Neo.FileStorage.Invoker.Morph
{
    public partial class MorphInvoker
    {
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

        public void PutContainer(FSContainer cnr, Signature sig, SessionToken token)
        {
            Invoke(ContainerContractHash, PutMethod, SideChainFee, cnr.ToByteArray(), sig.Sign.ToByteArray(), sig.Key.ToByteArray(), token?.ToByteArray() ?? System.Array.Empty<byte>());
        }

        public void SetEACL(EACLTable eacl, Signature sig, SessionToken token)
        {
            Invoke(ContainerContractHash, SetEACLMethod, SideChainFee, eacl.ToByteArray(), sig.Sign.ToByteArray(), sig.Key.ToByteArray(), token?.ToByteArray() ?? System.Array.Empty<byte>());
        }

        public void DeleteContainer(ContainerID cid, byte[] sig, SessionToken token)
        {
            Invoke(ContainerContractHash, DeleteMethod, SideChainFee, cid.Value.ToByteArray(), sig, token.ToByteArray());
        }

        public EAclWithSignature GetEACL(ContainerID containerID)
        {
            InvokeResult result = TestInvoke(ContainerContractHash, EACLMethod, containerID.Value.ToByteArray());
            Array array = (Array)result.ResultStack[0];
            if (array.Count != 4) throw new InvalidOperationException($"unexpected eacl stack item count, count={array.Count}");
            if (array[0].GetSpan().IsEmpty) throw new InvalidOperationException($"extended ACL table is not set for this container");
            return new()
            {
                Table = EACLTable.Parser.ParseFrom(array[0].GetSpan().ToArray()),
                Signature = new()
                {
                    Sign = GByteString.CopyFrom(array[1].GetSpan().ToArray()),
                    Key = GByteString.CopyFrom(array[2].GetSpan().ToArray())
                },
                SessionToken = array[3] is Null ? null : SessionToken.Parser.ParseFrom(array[3].GetSpan().ToArray())
            };
        }

        public ContainerWithSignature GetContainer(ContainerID containerID)
        {
            InvokeResult result = TestInvoke(ContainerContractHash, GetMethod, containerID.Value.ToByteArray());
            Array array = (Array)result.ResultStack[0];
            if (array.Count != 4) throw new InvalidOperationException($"unexpected container stack item, count={array.Count}");
            if (array[0].GetSpan().IsEmpty) throw new InvalidOperationException("container not found");
            ContainerWithSignature cnr = new()
            {
                Container = FSContainer.Parser.ParseFrom(array[0].GetSpan().ToArray()),
                Signature = new()
                {
                    Sign = GByteString.CopyFrom(array[1].GetSpan().ToArray()),
                    Key = GByteString.CopyFrom(array[2].GetSpan().ToArray())
                },
                SessionToken = array[3] is Null ? null : SessionToken.Parser.ParseFrom(array[3].GetSpan().ToArray())
            };
            return cnr;
        }

        public List<ContainerID> ListContainers(OwnerID ownerID)
        {
            InvokeResult result = TestInvoke(ContainerContractHash, ListMethod, ownerID.Value.ToByteArray());
            if (result.ResultStack[0] is Null) return new List<ContainerID>();
            Array array = (Array)result.ResultStack[0];
            List<byte[]> resultArray = new();
            foreach (StackItem current in array)
            {
                resultArray.Add(current.GetSpan().ToArray());
            }
            return resultArray.Select(p => ContainerID.FromValue(p)).ToList();
        }

        public void AnnounceLoad(Announcement announcement, byte[] key)
        {
            Invoke(ContainerContractHash, PutSizeMethod, SideChainFee, announcement.Epoch, announcement.ContainerId.Value.ToByteArray(), announcement.UsedSpace, key);
        }

        public Estimations GetContainerSize(ContainerID containerID)
        {
            InvokeResult result = TestInvoke(ContainerContractHash, GetSizeMethod, containerID.Value.ToByteArray());
            Array prms = (Array)result.ResultStack[0];
            Estimations es = new();
            es.ContainerID = ContainerID.FromValue(prms[0].GetSpan().ToArray());
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

        public List<byte[]> ListSizes(ulong epoch)
        {
            InvokeResult result = TestInvoke(ContainerContractHash, ListSizesMethod, epoch);
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

        public void StartEstimation(long epoch)
        {
            Invoke(ContainerContractHash, StartEstimationMethod, SideChainFee, epoch);
        }

        public void StopEstimation(long epoch)
        {
            Invoke(ContainerContractHash, StopEstimationMethod, SideChainFee, epoch);
        }

        public void RegisterContainer(byte[] key, byte[] container, byte[] signature, byte[] token)
        {
            Invoke(ContainerContractHash, PutMethod, SideChainFee, container, signature, key, token);
        }

        public void RemoveContainer(byte[] containerID, byte[] signature, byte[] token)
        {
            Invoke(ContainerContractHash, DeleteMethod, SideChainFee, containerID, signature, token);
        }
    }
}

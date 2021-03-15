using Google.Protobuf;
using NeoFS.API.v2.Client.ObjectParams;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.LocalObjectStorage;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;
using V2Client = NeoFS.API.v2.Client.Client;
using V2Object = NeoFS.API.v2.Object.Object;
using V2Range = NeoFS.API.v2.Object.Range;
using Neo.SmartContract.Native;
using Neo.FSNode.Services.Object.Get.Writer;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using System.IO;
using Neo.FSNode.Utils;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

namespace Neo.FSNode.Services.Object.Get
{
    public class Executor
    {
        public GetCommonPrm Prm;
        public GetService GetService;
        public V2Range Range;
        public bool HeadOnly;
        private bool assembly;
        private V2Object collectedObject;
        private SplitInfo splitInfo;
        private Traverser traverser;
        private ulong currentOffset;

        private bool ShouldWriteHeader => HeadOnly || Range is null;
        private bool ShouldWritePayload => !HeadOnly;
        private bool CanAsseble => assembly && !Prm.Raw && !HeadOnly;

        public void Execute()
        {
            ExecuteLocal();
            AnalyzeStatus(true);
        }

        private void ExecuteLocal()
        {
            collectedObject = GetService.LocalStorage.Get(Prm.Address);
            WriteCollectedObject();
        }

        private void MergeSplitInfo(SplitInfo info)
        {
            if (splitInfo is null)
            {
                splitInfo = info;
                return;
            }
            if (info.LastPart != null) splitInfo.LastPart = info.LastPart;
            if (info.Link != null) splitInfo.Link = info.Link;
            if (info.SplitId != null) splitInfo.SplitId = info.SplitId;
        }

        private (ObjectID, List<ObjectID>) InitFromChild(ObjectID oid)
        {
            var child = GetChild(oid, null, true);
            if (child is null) return (null, null);
            var parent = child.Parent();
            if (parent is null)
            {
                return (null, null);
            }
            collectedObject = parent;
            if (Range != null)
            {
                var seek_len = Range.Length;
                var seek_off = Range.Offset;
                var parent_size = parent.Header.PayloadLength;
                if (parent_size < seek_off + seek_len)
                {
                    throw new RangeOutOfBoundsException();
                }
                var child_size = child.Header.PayloadLength;
                currentOffset = parent_size - child_size;
                ulong from = 0;
                if (currentOffset < seek_off)
                    from = seek_off - currentOffset;
                ulong to = 0;
                if (currentOffset + from < seek_off + seek_len)
                    to = seek_off + seek_len - currentOffset;
                collectedObject.Payload = child.Payload.Range(from, to);
            }
            else
            {
                collectedObject.Payload = child.Payload;
            }
            return (child.Header.Split.Previous, child.Header.Split.Children.ToList());
        }

        private V2Object GetChild(ObjectID oid, V2Range range, bool with_header)
        {
            var writer = new SimpleObjectWriter();
            var prm = new GetCommonPrm
            {
                Address = new Address
                {
                    ContainerId = Prm.Address.ContainerId,
                    ObjectId = oid,
                },
                HeaderWriter = writer,
                ChunkWriter = writer,
            };
            prm.WithCommonPrm(Prm);
            prm.Local = false;
            GetService.Get(prm, range, false);
            var child = writer.Obj;
            if (with_header && !IsChild(child))
            {
                throw new Exception("wrong child header");
            }
            return child;
        }

        private bool IsChild(V2Object obj)
        {
            var parent = obj.Parent();
            return parent != null && parent.Address() == obj.Address();
        }

        private void Assemble()
        {
            if (!CanAsseble)
            {
                Utility.Log(nameof(Executor), LogLevel.Debug, "can not assembly the object");
                return;
            }
            Utility.Log(nameof(Executor), LogLevel.Debug, "trying to assemble the object...");
            var child_id = splitInfo.Link;
            if (child_id is null)
                child_id = splitInfo.LastPart;
            var result = InitFromChild(child_id);
            var prev = result.Item1;
            var children = result.Item2;
            if (children != null && 0 < children.Count)
            {
                if (Range is null)
                {
                    if (WriteCollectedHeader())
                    {
                        OvertakePayloadDirectly(children, null, false);
                    }
                }
                else
                {
                    if (OvertakePayloadInReverse(children[^1]))
                    {
                        WriteObjectPayload(collectedObject);
                    }
                }
            }
            else if (prev != null)
            {
                if (WriteCollectedHeader())
                {
                    if (OvertakePayloadInReverse(prev))
                    {
                        WriteObjectPayload(collectedObject);
                    }
                }
            }
            else
            {
                Utility.Log(nameof(Executor), LogLevel.Debug, " could not init parent from child");
            }
        }

        private void OvertakePayloadDirectly(List<ObjectID> children, List<V2Range> ranges, bool check_right)
        {
            var with_range = 0 < ranges.Count && Range != null;
            for (int i = 0; i < children.Count; i++)
            {
                V2Range r = null;
                if (with_range) r = ranges[i];
                var child = GetChild(children[i], r, !with_range && check_right);
                if (child is null) return;
                if (!WriteObjectPayload(child)) return;
            }
        }

        private bool OvertakePayloadInReverse(ObjectID prev)
        {
            if (BuildChainInReverse(prev, out List<ObjectID> oids, out List<V2Range> ranges))
            {
                if (0 < ranges.Count) ranges.Reverse();
                OvertakePayloadDirectly(oids, ranges, false);
            }
            return false;
        }

        private V2Object HeadChild(ObjectID oid)
        {
            var child_addr = new Address
            {
                ContainerId = Prm.Address.ContainerId,
                ObjectId = oid,
            };
            var prm = new HeadPrm();
            prm.WithGetCommonPrm(Prm);
            prm.Local = false;
            prm.Address = child_addr;
            prm.Short = false;
            var writer = new SimpleObjectWriter();
            prm.HeaderWriter = writer;
            GetService.Head(prm);
            var child = writer.Obj;
            if (child.Parent().ObjectId != null && IsChild(child))
            {
                Utility.Log(nameof(Executor), LogLevel.Info, "parent address in child object differs");
                return null;
            }
            return child;
        }

        private bool BuildChainInReverse(ObjectID prev, out List<ObjectID> oids, out List<V2Range> ranges)
        {
            var from = Range.Offset;
            var to = from + Range.Length;
            oids = new List<ObjectID>();
            ranges = new List<V2Range>();
            while (prev != null)
            {
                if (currentOffset < from) break;
                var head = HeadChild(prev);
                if (head is null) return false;
                if (Range != null)
                {
                    var sz = head.Header.PayloadLength;
                    currentOffset -= sz;
                    if (currentOffset < to)
                    {
                        var off = 0ul;
                        if (currentOffset < from)
                        {
                            off = from - currentOffset;
                            sz -= from - currentOffset;
                        }
                        if (to < currentOffset + off + sz)
                            sz = to - off - currentOffset;
                        var r = new V2Range
                        {
                            Offset = off,
                            Length = sz,
                        };
                        ranges.Add(r);
                        oids.Add(head.ObjectId);
                    }
                }
                else
                {
                    oids.Add(head.ObjectId);
                }
                prev = head.Header.Split.Previous;
            }
            return true;
        }

        private void AnalyzeStatus(bool exec_container)
        {
            switch (status)
            {
                case Status.Ok:
                    Utility.Log(nameof(Executor), LogLevel.Debug, "operation finished successfully");
                    break;
                case Status.INHUMED:
                    Utility.Log(nameof(Executor), LogLevel.Debug, "requested object was marked as removed");
                    break;
                case Status.VIRTUAL:
                    Utility.Log(nameof(Executor), LogLevel.Debug, "requested object is virtual");
                    Assemble();
                    break;
                case Status.OutOfRange:
                    Utility.Log(nameof(Executor), LogLevel.Debug, "requested range is out of object bounds");
                    break;
                default:
                    Utility.Log(nameof(Executor), LogLevel.Debug, "operation finished with exception " + exception.Message);
                    if (exec_container)
                    {
                        ExecuteOnContainer();
                        AnalyzeStatus(false);
                    }
                    break;
            }
        }

        private void WriteCollectedObject()
        {
            WriteCollectedHeader();
            WriteObjectPayload(collectedObject);
        }

        private void WriteCollectedHeader()
        {
            if (!ShouldWriteHeader) return;
            var cut_obj = V2Object.Parser.ParseFrom(collectedObject.ToByteArray());
            cut_obj.Payload = null;
            Prm.HeaderWriter.WriteHeader(cut_obj);
        }

        private void WriteObjectPayload(V2Object obj)
        {
            if (!ShouldWritePayload) return;
            Prm.ChunkWriter.WriteChunk(obj.Payload.ToByteArray());
        }

        private void ExecuteOnContainer()
        {
            if (Prm.Local) return;
            traverser = GenerateTraverser(Prm.Address);
            while (true)
            {
                var addrs = traverser.Next();
                if (addrs.Length == 0)
                {
                    Utility.Log(nameof(Executor), LogLevel.Debug, " no more nodes, abort placement iteration");
                    break;
                }
                foreach (var addr in addrs)
                {
                    if (ProcessNode(addr))
                    {
                        Utility.Log(nameof(ExecuteOnContainer), LogLevel.Debug, " completing the operation");
                        break;
                    }
                }
            }
        }

        private bool ProcessNode(Network.Address address)
        {
            var client = RemoteClient(address);
            try
            {
                collectedObject = client.GetObject(Prm.Context, new GetObjectParams { Address = Prm.Address, Raw = Prm.Raw }).Result;
            }
            catch (Exception e)
            {
                if (e is )
            }

        }

        private V2Client RemoteClient(Network.Address address)
        {
            var iport = address.IPAddressString();
            return GetService.ClientCache.GetClient(Prm.Key, iport);
        }

        private Traverser GenerateTraverser(Address address)
        {
            return GetService.TraverserGenerator.GenerateTraverser(address);
        }
    }
}

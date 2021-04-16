using Google.Protobuf;
using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.Object.Get.Writer;
using Neo.FileStorage.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Utility;
using FSClient = Neo.FileStorage.API.Client.Client;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Object.Get
{
    public partial class ExecuteContext
    {
        public GetCommonPrm Prm;
        public GetService GetService;
        public FSRange Range;
        public bool HeadOnly;

        private ulong currentEpoch;
        private bool assembly;
        private FSObject collectedObject;
        private SplitInfo splitInfo;
        private Traverser traverser;
        private ulong currentOffset;

        private bool ShouldWriteHeader => HeadOnly || Range is null;
        private bool ShouldWritePayload => !HeadOnly;
        private bool CanAssemble => assembly && !Prm.Raw && !HeadOnly;

        public void Execute()
        {
            try
            {
                ExecuteLocal();
            }
            catch (Exception le)
            {
                Log("GetExecutor", LogLevel.Debug, "local:" + le.Message);
                if (Prm.Local)
                    throw;
                ExecuteOnContainer();
            }
        }

        private (ObjectID, List<ObjectID>) InitFromChild(ObjectID oid)
        {
            var child = GetChild(oid, null, true);
            if (child is null) return (null, null);
            var parent = child.Parent;
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

        private FSObject GetChild(ObjectID oid, FSRange range, bool with_header)
        {
            var writer = new SimpleObjectWriter();
            var prm = new GetCommonPrm
            {
                Address = new Address
                {
                    ContainerId = Prm.Address.ContainerId,
                    ObjectId = oid,
                },
                Writer = writer,
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

        private bool IsChild(FSObject obj)
        {
            var parent = obj.Parent;
            return parent != null && parent.Address == obj.Address;
        }

        private void Assemble()
        {
            if (!CanAssemble)
            {
                Log(nameof(ExecuteContext), LogLevel.Debug, "can not assembly the object");
                return;
            }
            Log(nameof(ExecuteContext), LogLevel.Debug, "trying to assemble the object...");
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
                Log(nameof(ExecuteContext), LogLevel.Debug, " could not init parent from child");
            }
        }

        private void OvertakePayloadDirectly(List<ObjectID> children, List<FSRange> ranges, bool check_right)
        {
            var with_range = 0 < ranges.Count && Range != null;
            for (int i = 0; i < children.Count; i++)
            {
                FSRange r = null;
                if (with_range) r = ranges[i];
                var child = GetChild(children[i], r, !with_range && check_right);
                if (child is null) return;
                if (!WriteObjectPayload(child)) return;
            }
        }

        private bool OvertakePayloadInReverse(ObjectID prev)
        {
            if (BuildChainInReverse(prev, out List<ObjectID> oids, out List<FSRange> ranges))
            {
                if (0 < ranges.Count) ranges.Reverse();
                OvertakePayloadDirectly(oids, ranges, false);
            }
            return false;
        }

        private FSObject HeadChild(ObjectID oid)
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
            prm.Writer = writer;
            GetService.Head(prm);
            var child = writer.Obj;
            if (child.Parent.ObjectId != null && IsChild(child))
            {
                Log(nameof(ExecuteContext), LogLevel.Info, "parent address in child object differs");
                return null;
            }
            return child;
        }

        private bool BuildChainInReverse(ObjectID prev, out List<ObjectID> oids, out List<FSRange> ranges)
        {
            var from = Range.Offset;
            var to = from + Range.Length;
            oids = new List<ObjectID>();
            ranges = new List<FSRange>();
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
                        var r = new FSRange
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

        private void WriteCollectedObject()
        {
            WriteCollectedHeader();
            WriteObjectPayload(collectedObject);
        }

        private bool WriteCollectedHeader()
        {
            if (!ShouldWriteHeader) return true;
            var cut_obj = FSObject.Parser.ParseFrom(collectedObject.ToByteArray());
            cut_obj.Payload = null;
            Prm.Writer.WriteHeader(cut_obj);
            return true;
        }

        private bool WriteObjectPayload(FSObject obj)
        {
            if (!ShouldWritePayload) return true;
            Prm.Writer.WriteChunk(obj.Payload.ToByteArray());
            return true;
        }

        private Traverser GenerateTraverser(Address address)
        {
            return GetService.TraverserGenerator.GenerateTraverser(address);
        }

        private void InitEpoch()
        {
            currentEpoch = Prm.NetmapEpoch;
            if (0 < currentEpoch) return;
            currentEpoch = MorphContractInvoker.InvokeEpoch(GetService.MorphClient);
        }
    }
}

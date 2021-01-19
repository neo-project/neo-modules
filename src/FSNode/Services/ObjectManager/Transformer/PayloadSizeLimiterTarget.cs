using Google.Protobuf;
using NeoFS.API.v2.Refs;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using static NeoFS.API.v2.Object.Header.Types;
using V2Attribute = NeoFS.API.v2.Object.Header.Types.Attribute;
using V2Object = NeoFS.API.v2.Object.Object;


namespace Neo.FSNode.Services.ObjectManager.Transformer
{
    public class PayloadSizeLimiterTarget : IObjectTarget
    {
        private ulong maxSize;
        private ulong written;
        private IObjectTarget target;
        private V2Object current;
        private V2Object parent;
        private PayloadChecksumHasher[] currentHashers;
        private PayloadChecksumHasher[] parentHashers;
        private ObjectID[] previous;

        //private StreamWriter
        private BinaryWriter chunkWriter;
        private Guid splitID;
        private V2Attribute[] parAttrs;

        public PayloadSizeLimiterTarget(ulong maxSz)
        {
            maxSize = maxSz;
            splitID = Guid.NewGuid();
        }

        public void WriteHeader(V2Object obj)
        {
            current = FromObject(obj);
            Initialize();
        }

        public int Write(byte[] p)
        {
            WriteChunk(p);
            return p.Length;
        }

        public AccessIdentifiers Close()
        {
            return Release(true);
        }

        private void Initialize()
        {
            var len = previous.Length;
            if (len > 0)
            {
                if (len == 1)
                {
                    parent = current;
                    parent.Header.Split = null; // resetRelations
                    parentHashers = currentHashers;
                    current = parent;
                }

                current.Header.Split.Previous = previous[len - 1];
            }

            InitializeCurrent();
        }

        private V2Object FromObject(V2Object obj)
        {
            var res = new V2Object();
            res.Header.ContainerId = obj.Header.ContainerId;
            res.Header.OwnerId = obj.Header.OwnerId;
            res.Header.Attributes.AddRange(obj.Header.Attributes);
            res.Header.ObjectType = obj.Header.ObjectType;

            if (obj.Header.Split.SplitId != null)
                res.Header.Split.SplitId = obj.Header.Split.SplitId;

            return res;
        }

        private void InitializeCurrent()
        {
            // initialize current object target
            target = new FormatterTarget();
            // create payload hashers
            currentHashers = PayloadHashersForObject(current);

            // TBD, add writer
            // compose multi-writer from target and all payload hashers
        }

        private PayloadChecksumHasher[] PayloadHashersForObject(V2Object obj)
        {
            return new PayloadChecksumHasher[] { }; // TODO, need TzHash dependency
        }

        private AccessIdentifiers Release(bool close)
        {
            // Arg close is true only from Close method.
            // We finalize parent and generate linking objects only if it is more
            // than 1 object in split-chain
            var withParent = close && previous.Length > 0;

            if (withParent)
            {
                WriteHashes(parentHashers);
                parent.Header.PayloadLength = written;
                current.Header.Split.Parent = parent.ObjectId;
            }
            // release current object
            WriteHashes(currentHashers);
            // release current
            target.WriteHeader(current);

            var ids = target.Close();
            previous = previous.Append(ids.Self).ToArray();

            if (withParent)
            {
                InitializeLinking();
                InitializeCurrent();
                Release(false);
            }
            return ids;
        }
        private void WriteHashes(PayloadChecksumHasher[] hashers)
        {
            for (int i = 0; i < hashers.Length; i++)
            {
                hashers[i].ChecksumWriter(hashers[i].Hasher.Hash);
            }
        }

        private void InitializeLinking()
        {
            current = FromObject(current);
            current.Header.Split.Parent = parent.ObjectId;
            current.Header.Split.Children.AddRange(previous);
            current.Header.Split.SplitId = ByteString.CopyFrom(splitID.ToByteArray());
        }

        public void WriteChunk(byte[] chunk)
        {
            // statement is true if the previous write of bytes reached exactly the boundary.
            if (written > 0 && written % maxSize == 0)
            {
                if (written == maxSize)
                    PrepareFirstChild();

                // need to release current object
                Release(false);
                // initialize another object
                Initialize();
            }

            ulong len = (ulong)chunk.Length;
            var cut = len;
            var leftToEdge = maxSize - written % maxSize;

            if (len > leftToEdge)
                cut = leftToEdge;

            chunkWriter.Write(chunk[..(int)cut]);
            // increase written bytes counter
            written += cut;
            // if there are more bytes in buffer we call method again to start filling another object
            if (len > leftToEdge)
                WriteChunk(chunk[(int)cut..]);
        }

        private void PrepareFirstChild()
        {
            // initialize split header with split ID on first object in chain
            current.Header.Split = new Split(); // InitRelations
            current.Header.Split.SplitId = ByteString.CopyFrom(splitID.ToByteArray());

            // cut source attributes
            parAttrs = current.Header.Attributes.ToArray();
            current.Header.Attributes.Clear();

            // attributes will be added to parent in detachParent
        }

        private void DetachParent()
        {
            parent = current;
            current = FromObject(parent);
            parent.Header.Split = null; // reset relations
            parentHashers = currentHashers;

            parent.Header.Attributes.Clear();
            parent.Header.Attributes.AddRange(parAttrs);
        }
    }

    public delegate void ChecksumWriter(byte[] b);

    public class PayloadChecksumHasher
    {
        //private HashAlgorithm hasher;
        //private ChecksumWriter checksumWriter;

        public HashAlgorithm Hasher { get; set; }
        public ChecksumWriter ChecksumWriter { get; set; }
    }
}

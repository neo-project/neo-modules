using Google.Protobuf;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using FSAttribute = Neo.FileStorage.API.Object.Header.Types.Attribute;
using FSObject = Neo.FileStorage.API.Object.Object;


namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class PayloadSizeLimiterTarget : IObjectTarget
    {
        private readonly CancellationToken cancellation;
        private readonly ulong maxSize;
        private readonly IObjectTarget next;
        private readonly List<ObjectID> previous = new();
        private readonly SplitID splitID;
        private ulong written;
        private FSObject current;
        private FSObject parent;
        private IEnumerable<FSAttribute> parentAttributes;
        private SHA256[] sha256Hashers = new SHA256[] { SHA256.Create(), SHA256.Create() };
        private readonly HomomorphicHasher homomorphicHasher;

        public PayloadSizeLimiterTarget(ulong maxSz, IObjectTarget target, CancellationToken cancellation)
        {
            this.cancellation = cancellation;
            maxSize = maxSz;
            splitID = new();
            next = target;
            homomorphicHasher = new(cancellation);
        }

        public void WriteHeader(FSObject obj)
        {
            current = FromObject(obj);
            Initialize();
        }

        public AccessIdentifiers Close()
        {
            return Release(true);
        }

        public void Dispose()
        {
            next.Dispose();
            foreach (var hasher in sha256Hashers)
                hasher.Dispose();
            homomorphicHasher.Dispose();
        }

        private void Initialize()
        {
            if (0 < previous.Count)
            {
                if (previous.Count == 1)
                {
                    DetachParent();
                }
                current.Header.Split.Previous = previous[^1];
            }
        }

        private AccessIdentifiers Release(bool close)
        {
            var withParent = close && previous.Count > 0;
            if (withParent)
            {
                Calculatehash(parent, true);
                parent.Header.PayloadLength = written;
                current.Parent = parent;
            }
            Calculatehash(current, false);
            next.WriteHeader(current);
            var ids = next.Close();
            previous.Add(ids.Self);
            if (withParent)
            {
                InitializeLinking(ids.ParentHeader);
                Release(false);
            }
            return ids;
        }

        private void Calculatehash(FSObject obj, bool parent)
        {
            SHA256 sha256Hahser = sha256Hashers[parent ? 0 : 1];
            sha256Hahser.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            obj.PayloadChecksum = new()
            {
                Type = ChecksumType.Sha256,
                Sum = ByteString.CopyFrom(sha256Hahser.Hash),
            };
            obj.PayloadHomomorphicHash = new()
            {
                Type = ChecksumType.Tz,
                Sum = ByteString.CopyFrom(homomorphicHasher.Hash),
            };
        }

        private void InitializeLinking(FSObject parentHeader)
        {
            current = FromObject(current);
            current.Parent = parentHeader;
            current.Children = previous;
            current.SplitId = splitID;
        }

        public void WriteChunk(byte[] chunk)
        {
            if (written > 0 && written % maxSize == 0)
            {
                if (written == maxSize)
                    PrepareFirstChild();
                Release(false);
                Initialize();
            }
            var len = (ulong)chunk.Length;
            var cut = len;
            var leftToEdge = maxSize - written % maxSize;
            if (cut > leftToEdge)
                cut = leftToEdge;
            Write(chunk[..(int)cut]);
            written += cut;
            if (len > leftToEdge)
                WriteChunk(chunk[(int)cut..]);
        }

        private void Write(byte[] chunk)
        {
            foreach (var hasher in sha256Hashers)
                hasher.TransformBlock(chunk, 0, chunk.Length, null, 0);
            homomorphicHasher.WriteChunk(chunk);
            next.WriteChunk(chunk);
        }

        private void PrepareFirstChild()
        {
            current.SplitId = splitID;
            parentAttributes = current.Header.Attributes;
            current.Header.Attributes.Clear();
        }

        private void DetachParent()
        {
            parent = current.CutPayload();
            current = FromObject(parent);
            parent.Header.Split = null;
            parent.Signature = null;
            parent.Header.Attributes.Clear();
            parent.Header.Attributes.AddRange(parentAttributes);
        }

        private FSObject FromObject(FSObject obj)
        {
            FSObject r = new()
            {
                Header = new()
                {
                    ContainerId = obj.ContainerId,
                    OwnerId = obj.OwnerId,
                    ObjectType = obj.ObjectType,
                }
            };
            r.Header.Attributes.AddRange(obj.Attributes);
            if (obj.SplitId is not null)
                r.SplitId = obj.SplitId;
            return r;
        }
    }
}

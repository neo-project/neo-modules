using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using FSAttribute = Neo.FileStorage.API.Object.Header.Types.Attribute;
using FSObject = Neo.FileStorage.API.Object.Object;


namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class PayloadSizeLimiterTarget : IObjectTarget
    {
        private readonly ulong maxSize;
        private readonly IObjectTarget next;
        private readonly List<ObjectID> previous = new();
        private readonly SplitID splitID;
        private ulong written;
        private FSObject current;
        private FSObject parent;
        private IEnumerable<FSAttribute> parentAttributes;
        private HashAlgorithm[] currentHashers = Array.Empty<HashAlgorithm>();
        private HashAlgorithm[] parentHashers = Array.Empty<HashAlgorithm>();

        public PayloadSizeLimiterTarget(ulong maxSz, IObjectTarget t)
        {
            maxSize = maxSz;
            splitID = new();
            next = t;
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
            //Dispose hasher
        }

        private void Initialize()
        {
            if (previous.Any())
            {
                if (previous.Count == 1)
                {
                    DetachParent();
                }
                current.Header.Split.Previous = previous[^1];
            }
            InitializeCurrent();
        }

        private HashAlgorithm[] NewHasherPair()
        {
            var hasher1 = SHA256.Create();
            hasher1.Initialize();
            var hasher2 = new TzHash();
            hasher2.Initialize();
            return new HashAlgorithm[] { hasher1, hasher2 };
        }

        private void InitializeCurrent()
        {
            if (!currentHashers.Any())
            {
                parentHashers = NewHasherPair();
                currentHashers = NewHasherPair();
            }
            else
            {
                foreach (var hasher in currentHashers)
                    hasher.Initialize();
            }
        }

        private AccessIdentifiers Release(bool close)
        {
            var withParent = close && previous.Count > 0;
            if (withParent)
            {
                Calculatehash(parent, parentHashers);
                parent.Header.PayloadLength = written;
                current.Parent = parent;
            }
            Calculatehash(current, currentHashers);
            next.WriteHeader(current);
            var ids = next.Close();
            previous.Add(ids.Self);
            if (withParent)
            {
                InitializeLinking(ids.ParentHeader);
                InitializeCurrent();
                Release(false);
            }
            return ids;
        }

        private void Calculatehash(FSObject obj, HashAlgorithm[] hashers)
        {
            foreach (var hasher in hashers)
            {
                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                switch (hasher)
                {
                    case SHA256 sha256_hasher:
                        obj.PayloadChecksum = new()
                        {
                            Type = ChecksumType.Sha256,
                            Sum = ByteString.CopyFrom(sha256_hasher.Hash),
                        };
                        break;
                    case TzHash tz_hasher:
                        obj.PayloadHomomorphicHash = new()
                        {
                            Type = ChecksumType.Tz,
                            Sum = ByteString.CopyFrom(tz_hasher.Hash),
                        };
                        break;
                    default:
                        throw new InvalidOperationException($"{nameof(PayloadSizeLimiterTarget)} not supported checksum type");
                }
            }
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
            foreach (var hasher in currentHashers)
                hasher.TransformBlock(chunk, 0, chunk.Length, null, 0);
            foreach (var hasher in parentHashers)
                hasher.TransformBlock(chunk, 0, chunk.Length, null, 0);
            next.WriteChunk(chunk);
        }

        private void PrepareFirstChild()
        {
            current.Header.Split = new();
            current.SplitId = splitID;
            parentAttributes = current.Header.Attributes;
            current.Header.Attributes.Clear();
        }

        private void DetachParent()
        {
            parent = current;
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
            r.Attributes.AddRange(obj.Attributes);
            if (obj.SplitId is not null)
                r.SplitId = obj.SplitId;
            return r;
        }
    }
}

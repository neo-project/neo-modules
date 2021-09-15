using Neo.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Helper;

namespace Neo.Plugins.MPT
{
    partial class MPTTrie<TKey, TValue>
    {
        private ReadOnlySpan<byte> Seek(ref MPTNode node, ReadOnlySpan<byte> path, out MPTNode start)
        {
            switch (node.Type)
            {
                case NodeType.LeafNode:
                    {
                        if (path.IsEmpty)
                        {
                            start = node;
                            return ReadOnlySpan<byte>.Empty;
                        }
                        break;
                    }
                case NodeType.Empty:
                    break;
                case NodeType.HashNode:
                    {
                        var newNode = cache.Resolve(node.Hash);
                        if (newNode is null) throw new InvalidOperationException("Internal error, can't resolve hash when mpt seek");
                        node = newNode;
                        return Seek(ref node, path, out start);
                    }
                case NodeType.BranchNode:
                    {
                        if (path.IsEmpty)
                        {
                            start = node;
                            return ReadOnlySpan<byte>.Empty;
                        }
                        return Concat(path[..1], Seek(ref node.Children[path[0]], path[1..], out start));
                    }
                case NodeType.ExtensionNode:
                    {
                        if (path.IsEmpty)
                        {
                            start = node.Next;
                            return node.Key;
                        }
                        if (path.StartsWith(node.Key))
                        {
                            return Concat(node.Key, Seek(ref node.Next, path[node.Key.Length..], out start));
                        }
                        if (node.Key.AsSpan().StartsWith(path))
                        {
                            start = node.Next;
                            return node.Key;
                        }
                        break;
                    }
            }
            start = null;
            return ReadOnlySpan<byte>.Empty;
        }

        public IEnumerable<(TKey Key, TValue Value)> Find(ReadOnlySpan<byte> prefix, byte[] from = null)
        {
            var path = ToNibbles(prefix);
            if (from is null) from = Array.Empty<byte>();
            if (0 < from.Length)
            {
                if (!from.AsSpan().StartsWith(prefix))
                    throw new InvalidOperationException("invalid from key");
                from = ToNibbles(from[prefix.Length..].AsSpan());
            }
            path = Seek(ref root, path, out MPTNode start).ToArray();
            return Travers(start, path, from)
                .Select(p => (FromNibbles(p.Key).AsSerializable<TKey>(), p.Value.AsSerializable<TValue>()));
        }

        private IEnumerable<(byte[] Key, byte[] Value)> Travers(MPTNode node, byte[] path, byte[] from)
        {
            if (node is null) yield break;
            switch (node.Type)
            {
                case NodeType.LeafNode:
                    {
                        if (from.Length == 0)
                            yield return (path, (byte[])node.Value.Clone());
                        break;
                    }
                case NodeType.Empty:
                    break;
                case NodeType.HashNode:
                    {
                        var newNode = cache.Resolve(node.Hash);
                        if (newNode is null) throw new InvalidOperationException("Internal error, can't resolve hash when mpt find");
                        node = newNode;
                        foreach (var item in Travers(node, path, from))
                            yield return item;
                        break;
                    }
                case NodeType.BranchNode:
                    {
                        if (0 < from.Length)
                        {
                            for (int i = 0; i < MPTNode.BranchChildCount - 1; i++)
                            {
                                if (from[0] < i)
                                    foreach (var item in Travers(node.Children[i], Concat(path, new byte[] { (byte)i }), Array.Empty<byte>()))
                                        yield return item;
                                else if (i == from[0])
                                    foreach (var item in Travers(node.Children[i], Concat(path, new byte[] { (byte)i }), from[1..]))
                                        yield return item;
                            }
                        }
                        else
                        {
                            foreach (var item in Travers(node.Children[MPTNode.BranchChildCount - 1], path, Array.Empty<byte>()))
                                yield return item;
                            for (int i = 0; i < MPTNode.BranchChildCount - 1; i++)
                            {
                                foreach (var item in Travers(node.Children[i], Concat(path, new byte[] { (byte)i }), Array.Empty<byte>()))
                                    yield return item;
                            }
                        }
                        break;
                    }
                case NodeType.ExtensionNode:
                    {
                        if (from.AsSpan().StartsWith(node.Key))
                            foreach (var item in Travers(node.Next, Concat(path, node.Key), from[node.Key.Length..]))
                                yield return item;
                        else if (0 == from.Length || 0 < node.Key.CompareTo(from))
                            foreach (var item in Travers(node.Next, Concat(path, node.Key), Array.Empty<byte>()))
                                yield return item;
                        break;
                    }
            }
        }
    }
}

using Neo.IO;
using System;

namespace Neo.Plugins.MPT
{
    partial class MPTTrie<TKey, TValue>
    {
        private static ReadOnlySpan<byte> CommonPrefix(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            var minLen = a.Length <= b.Length ? a.Length : b.Length;
            int i = 0;
            if (a.Length != 0 && b.Length != 0)
            {
                for (i = 0; i < minLen; i++)
                {
                    if (a[i] != b[i]) break;
                }
            }
            return a[..i];
        }

        public void Put(TKey key, TValue value)
        {
            var path = ToNibbles(key.ToArray());
            var val = value.ToArray();
            if (path.Length == 0 || path.Length > MPTNode.MaxKeyLength)
                throw new ArgumentException("invalid", nameof(key));
            if (val.Length > MPTNode.MaxValueLength)
                throw new ArgumentException("exceed limit", nameof(value));
            var n = MPTNode.NewLeaf(val);
            Put(ref root, path, n);
        }

        private void Put(ref MPTNode node, ReadOnlySpan<byte> path, MPTNode val)
        {
            switch (node.Type)
            {
                case NodeType.LeafNode:
                    {
                        if (path.IsEmpty)
                        {
                            if (!full) cache.DeleteNode(node.Hash);
                            node = val;
                            cache.PutNode(node);
                            return;
                        }
                        var branch = MPTNode.NewBranch();
                        branch.Children[MPTNode.BranchChildCount - 1] = node;
                        Put(ref branch.Children[path[0]], path[1..], val);
                        cache.PutNode(branch);
                        node = branch;
                        break;
                    }
                case NodeType.ExtensionNode:
                    {
                        if (path.StartsWith(node.Key))
                        {
                            var oldHash = node.Hash;
                            Put(ref node.Next, path[node.Key.Length..], val);
                            if (!full) cache.DeleteNode(oldHash);
                            node.SetDirty();
                            cache.PutNode(node);
                            return;
                        }
                        if (!full) cache.DeleteNode(node.Hash);
                        var prefix = CommonPrefix(node.Key, path);
                        var pathRemain = path[prefix.Length..];
                        var keyRemain = node.Key.AsSpan(prefix.Length);
                        var child = MPTNode.NewBranch();
                        MPTNode grandChild = new MPTNode();
                        if (keyRemain.Length == 1)
                        {
                            child.Children[keyRemain[0]] = node.Next;
                        }
                        else
                        {
                            var exNode = MPTNode.NewExtension(keyRemain[1..].ToArray(), node.Next);
                            cache.PutNode(exNode);
                            child.Children[keyRemain[0]] = exNode;
                        }
                        if (pathRemain.IsEmpty)
                        {
                            Put(ref grandChild, pathRemain, val);
                            child.Children[MPTNode.BranchChildCount - 1] = grandChild;
                        }
                        else
                        {
                            Put(ref grandChild, pathRemain[1..], val);
                            child.Children[pathRemain[0]] = grandChild;
                        }
                        cache.PutNode(child);
                        if (prefix.Length > 0)
                        {
                            var exNode = MPTNode.NewExtension(prefix.ToArray(), child);
                            cache.PutNode(exNode);
                            node = exNode;
                        }
                        else
                        {
                            node = child;
                        }
                        break;
                    }
                case NodeType.BranchNode:
                    {
                        var oldHash = node.Hash;
                        if (path.IsEmpty)
                        {
                            Put(ref node.Children[MPTNode.BranchChildCount - 1], path, val);
                        }
                        else
                        {
                            Put(ref node.Children[path[0]], path[1..], val);
                        }
                        if (!full) cache.DeleteNode(oldHash);
                        node.SetDirty();
                        cache.PutNode(node);
                        break;
                    }
                case NodeType.Empty:
                    {
                        MPTNode newNode;
                        if (path.IsEmpty)
                        {
                            newNode = val;
                        }
                        else
                        {
                            newNode = MPTNode.NewExtension(path.ToArray(), val);
                            cache.PutNode(newNode);
                        }
                        node = newNode;
                        if (val.Type == NodeType.LeafNode) cache.PutNode(val);
                        break;
                    }
                case NodeType.HashNode:
                    {
                        MPTNode newNode = cache.Resolve(node.Hash);
                        if (newNode is null) throw new InvalidOperationException("Internal error, can't resolve hash when mpt put");
                        node = newNode;
                        Put(ref node, path, val);
                        break;
                    }
                default:
                    throw new InvalidOperationException("unsupport node type");
            }
        }
    }
}

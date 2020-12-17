using Neo.IO.Caching;

namespace Neo.Plugins.MPT
{
    public enum NodeType : byte
    {
        [ReflectionCache(typeof(BranchNode))]
        BranchNode = 0x00,
        [ReflectionCache(typeof(ExtensionNode))]
        ExtensionNode = 0x01,
        LeafNode = 0x02,
        HashNode = 0x03,
        Empty = 0x04
    }
}

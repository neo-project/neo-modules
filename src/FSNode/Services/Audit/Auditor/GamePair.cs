using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Audit.Auditor
{
    public class GamePair
    {
        public Node N1;
        public Node N2;
        public ObjectID Id;
        public List<Range> Range1;
        public List<Range> Range2;
        public List<byte[]> Hashes1;
        public List<byte[]> Hashes2;
    }
}

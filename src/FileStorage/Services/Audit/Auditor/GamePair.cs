using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Audit.Auditor
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

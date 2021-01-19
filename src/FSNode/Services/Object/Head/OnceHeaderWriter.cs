using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Services.ObjectManager.Placement;
using System.Threading;

namespace Neo.FSNode.Services.Object.Head
{
    public class OnceHeaderWriter
    {
        private bool writed = false;
        public CancellationTokenSource TokenSource;
        public Traverser Traverser;
        public HeadResult Result;


        public void Write(V2Object header)
        {
            if (writed) return;
            if (header is null) return;
            Result.Header = header;
            Traverser.SubmitSuccess();
            TokenSource.Cancel();
            writed = true;
        }
    }
}
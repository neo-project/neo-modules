using Neo.FileStorage.Services.ObjectManager.Placement;

namespace Neo.FileStorage.Services.Object.Search.Execute
{
    public partial class ExecuteContext
    {
        public SearchPrm Prm { get; init; }
        public SearchService SearchService { get; init; }

        private ulong currentEpoch;
        private Traverser traverser;

        public void Execute()
        {
            ExecuteLocal();
            if (!Prm.Local)
                ExecuteContainer();
        }
    }
}

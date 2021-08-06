using Neo.FileStorage.Invoker.Morph;

namespace Neo.FileStorage.Storage.Services.Object.Search
{
    public class EpochSource : IEpochSource
    {
        private readonly MorphInvoker morphInvoker;

        public ulong CurrentEpoch => morphInvoker.Epoch();

        public EpochSource(MorphInvoker invoker)
        {
            morphInvoker = invoker;
        }
    }
}

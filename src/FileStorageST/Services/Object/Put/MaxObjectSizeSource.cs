using Neo.FileStorage.Invoker.Morph;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public class MaxObjectSizeSource : IMaxObjectSizeSource
    {
        private readonly MorphInvoker morphInvoker;

        public MaxObjectSizeSource(MorphInvoker invoker)
        {
            morphInvoker = invoker;
        }

        public ulong MaxObjectSize()
        {
            return morphInvoker.MaxObjectSize();
        }
    }
}

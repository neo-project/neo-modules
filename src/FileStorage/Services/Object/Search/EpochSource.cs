using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Object.Search
{
    public class EpochSource : IEpochSource
    {
        private readonly Client morphClient;

        public EpochSource(Client morph)
        {
            morphClient = morph;
        }

        ulong IEpochSource.CurrentEpoch()
        {
            return morphClient.InvokeEpoch();
        }
    }
}

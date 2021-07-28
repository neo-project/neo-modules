using System.Threading;
using System.Threading.Tasks;

namespace Neo.FileStorage.Storage.Services.Police
{
    internal class PoliceTask
    {
        public CancellationTokenSource Source;
        public Task Task;
        public int Undone;
    }
}

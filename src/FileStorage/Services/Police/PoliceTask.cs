using System.Threading;
using System.Threading.Tasks;

namespace Neo.FileStorage.Services.Police
{
    internal class PoliceTask
    {
        public CancellationTokenSource Cancellation;
        public Task Task;
        public int Undone;
    }
}

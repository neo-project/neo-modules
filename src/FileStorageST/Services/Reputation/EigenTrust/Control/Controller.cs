using Akka.Actor;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Calculate;
using Neo.FileStorage.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Control
{
    public class Controller
    {
        private readonly MorphInvoker morphInvoker;
        private readonly Calculator daughterTrustCalculator;
        private readonly IActorRef workerPool;
        private uint iterationNumber;
        private readonly ConcurrentDictionary<ulong, IterationContextCancellable> ctxs = new();

        public Controller(MorphInvoker invoker, Calculator calculator, IActorRef wp)
        {
            morphInvoker = invoker;
            daughterTrustCalculator = calculator;
            workerPool = wp;
            iterationNumber = (uint)morphInvoker.EigenTrustIterations();
        }

        public void Continue(ulong epoch)
        {
            if (!ctxs.TryGetValue(epoch, out IterationContextCancellable context))
            {
                CancellationTokenSource cancellationSource = new();
                context = new()
                {
                    Context = new()
                    {
                        Cancellation = cancellationSource.Token,
                        Epoch = epoch,
                    },
                    CancellationSource = cancellationSource,
                };
                ctxs[epoch] = context;
            }
            context.Context.Last = context.Context.Index == iterationNumber - 1;
            var r = workerPool.Ask<bool>(new WorkerPool.NewTask
            {
                Process = "Reputation",
                Task = new(() =>
                {
                    daughterTrustCalculator.Calculate(context.Context);
                    if (context.Context.Last)
                    {
                        context.Dispose();
                        return;
                    }
                    context.Context.Index++;
                }),
            }).Result;
            if (!r)
                Utility.Log("EigenTrustController", LogLevel.Warning, "worker pool submit failure");
            if (context.Context.Last)
            {
                ctxs.Remove(epoch, out _);
                var iterations = morphInvoker.EigenTrustIterations();
                iterationNumber = (uint)iterations;
                Utility.Log("EigenTrustController", LogLevel.Debug, "update iteration number " + iterationNumber);
            }
        }
    }
}

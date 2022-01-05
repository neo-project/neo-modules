using Akka.Actor;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Reputaion.Common.Route;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Daughters;
using Neo.FileStorage.Utils;
using System;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Calculate
{
    public class Calculator
    {
        public const int DefaultWorkPoolSize = 32;

        public MorphInvoker MorphInvoker { get; init; }
        public InitialTrustSource InitialTrustSource { get; init; }
        public DaughterTrustIteratorProvider DaughterTrustSource { get; init; }
        public Router IntermediateValueTarget { get; init; }
        public FinalWriterProvider FinalWriteTarget { get; init; }
        public IActorRef WorkerPool { get; init; }
        private double alpha;
        private double beta;

        public void Calculate(IterationContext context)
        {
            Utility.Log(nameof(Calculator), LogLevel.Debug, $"calculating eigentrust, epoch={context.Epoch}, index={context.Index}, last={context.Last}");
            alpha = MorphInvoker.EigenTrustAlpha();
            beta = 1 - alpha;
            if (context.Index == 0)
            {
                SendInitValues(context);
                return;
            }
            var index = context.Index;
            context.Index = index - 1;
            ConsumerStorage consumers;
            try
            {
                consumers = DaughterTrustSource.InitConsumerIterator(context);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Calculator), LogLevel.Warning, e.Message);
                return;
            }
            context.Index = index;
            consumers.Iterate((p, trusts) =>
            {
                var r = WorkerPool.Ask<bool>(new WorkerPool.NewTask
                {
                    Process = "Reputation",
                    Task = new(() =>
                    {
                        IterateDaughter(context, p, trusts, context.Last);
                    }),
                }).Result;
                if (!r)
                    Utility.Log(nameof(Calculator), LogLevel.Warning, $"worker pool submit failure");
            });
        }

        private void SendInitValues(IterationContext context)
        {
            DaughterStorage daughters;
            try
            {
                daughters = DaughterTrustSource.InitAllDaughtersIterator(context);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(SendInitValues), LogLevel.Warning, e.Message);
                return;
            }
            var intermediate_writer = IntermediateValueTarget.InitWriter(context);
            daughters.Iterate((p, trusts) =>
            {
                trusts.Iterate(t =>
                {
                    try
                    {
                        var init_t = InitialTrustSource.InitialTrust(p);
                        t.Trust.Value *= init_t;
                        intermediate_writer.Write(t);
                    }
                    catch
                    {
                        return;
                    }
                });
            });
            intermediate_writer.Close();
        }

        private void IterateDaughter(IterationContext context, PeerID peer, ConsumerTrusts trusts, bool last)
        {
            double init;
            try
            {
                init = InitialTrustSource.InitialTrust(peer);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(IterateDaughter), LogLevel.Warning, $"get initial trust failure, peer={peer.PublicKey.ToBase64()}, error={e.Message}");
                return;
            }
            DaughterTrusts daughters;
            try
            {
                daughters = DaughterTrustSource.InitDaughterIterator(context, peer);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(IterateDaughter), LogLevel.Warning, e.Message);
                return;
            }
            double sum = 0;
            try
            {
                trusts.Iterate(t =>
                {
                    if (!last)
                        context.Cancellation.ThrowIfCancellationRequested();
                    sum += t.Trust.Value;
                });
            }
            catch (Exception e)
            {
                Utility.Log(nameof(IterateDaughter), LogLevel.Warning, $"could not sum trusts, error={e.Message}");
                return;
            }
            init *= alpha;
            sum *= beta;
            IterationTrust it = new()
            {
                Epoch = context.Epoch,
                Index = context.Index,
                Trust = new()
                {
                    Trust = new()
                    {
                        Peer = peer,
                    }
                }
            };
            if (last)
            {
                try
                {
                    var final_writer = FinalWriteTarget.InitIntermediateWriter();
                    it.Trust.Trust.Value = sum;
                    final_writer.WriteIntermediateTrust(it);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(IterateDaughter), LogLevel.Warning, $"write final result failure, error={e.Message}");
                    return;
                }
            }
            else
            {
                try
                {
                    var intermediate_writer = IntermediateValueTarget.InitWriter(context);
                    daughters.Iterate(t =>
                    {
                        context.Cancellation.ThrowIfCancellationRequested();
                        t.Trust.Value *= sum;
                        intermediate_writer.Write(t);
                    });
                    intermediate_writer.Close();
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(IterateDaughter), LogLevel.Warning, $"intermediate target write failure, error={e.Message}");
                    return;
                }
            }
        }
    }
}

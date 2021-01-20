using Akka.Actor;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Plugins.StateService.Storage;
using Neo.Wallets;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Plugins.StateService.Verification
{
    public class VerificationService : UntypedActor
    {
        public class ValidatedRootPersisted { public uint Index; }
        public class BlockPersisted { public uint Index; }
        private class Timer { public uint Index; }
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private readonly NeoSystem core;
        private const int MaxCachedVerificationProcessCount = 10;
        private readonly Wallet wallet;
        private readonly ConcurrentDictionary<uint, VerificationContext> contexts = new ConcurrentDictionary<uint, VerificationContext>();

        public VerificationService(NeoSystem core, Wallet wallet)
        {
            this.core = core;
            this.wallet = wallet;
        }

        private void SendVote(VerificationContext context)
        {
            var vote = context.CreateVote();
            if (vote is null) return;
            Utility.Log(nameof(VerificationService), LogLevel.Info, $"relay vote");
            Parallel.ForEach(Settings.Default.VerifierUrls, (url, state, i) =>
            {
                try
                {
                    var client = new RpcClient(url);
                    client?.RpcSendAsync("votestateroot", vote.RootIndex, vote.ValidatorIndex, vote.Signature.ToHexString())
                        .GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(VerificationService), LogLevel.Warning, $"Failed to send vote, validator={url}, error={e.Message}");
                }
            });
            CheckVotes(context);
        }

        private void OnVoteStateRoot(Vote vote)
        {
            if (contexts.TryGetValue(vote.RootIndex, out VerificationContext context) && context.AddSignature(vote.ValidatorIndex, vote.Signature))
            {
                CheckVotes(context);
            }
        }

        private void CheckVotes(VerificationContext context)
        {
            if (context.CheckSignatures())
            {
                if (context.Message is null) return;
                var state_root = context.Message.Data.AsSerializable<StateRoot>();
                Utility.Log(nameof(VerificationService), LogLevel.Info, $"relay state root, height={state_root.Index}, root={state_root.RootHash}");
                core.Blockchain.Tell(context.Message);
            }
        }

        private void OnBlockPersisted(uint index)
        {
            if (MaxCachedVerificationProcessCount <= contexts.Count)
            {
                var indexes = contexts.Keys.OrderBy(i => i).ToArray();
                while (MaxCachedVerificationProcessCount <= indexes.Length)
                {
                    if (contexts.TryRemove(indexes[0], out var value))
                    {
                        value.Timer.CancelIfNotNull();
                        indexes = indexes[..1];
                    }
                }
            }
            var p = new VerificationContext(wallet, index);
            if (p.IsValidator && contexts.TryAdd(index, p))
            {
                p.Timer = Context.System.Scheduler.ScheduleTellOnceCancelable(Timeout, Self, new Timer
                {
                    Index = index,
                }, ActorRefs.NoSender);
                Utility.Log(nameof(VerificationContext), LogLevel.Info, $"new validate process, height={index}, index={p.MyIndex}, ongoing={contexts.Count}");
                SendVote(p);
            }
        }

        private void OnValidatedRootPersisted(uint index)
        {
            Utility.Log(nameof(VerificationService), LogLevel.Info, $"persisted state root, height={index}");
            foreach (var i in contexts.Where(i => i.Key <= index))
            {
                if (contexts.TryRemove(i.Key, out var value))
                {
                    value.Timer.CancelIfNotNull();
                }
            }
        }

        private void OnTimer(uint index)
        {
            if (contexts.TryGetValue(index, out VerificationContext context))
            {
                SendVote(context);
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Vote v:
                    OnVoteStateRoot(v);
                    break;
                case BlockPersisted bp:
                    OnBlockPersisted(bp.Index);
                    break;
                case ValidatedRootPersisted root:
                    OnValidatedRootPersisted(root.Index);
                    break;
                case Timer timer:
                    OnTimer(timer.Index);
                    break;
                default:
                    break;
            }
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        public static Props Props(NeoSystem core, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new VerificationService(core, wallet));
        }
    }
}

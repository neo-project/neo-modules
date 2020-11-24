using Akka.Actor;
using Neo.Network.RPC;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.MPTService.Validation
{
    public class ValidationService : UntypedActor
    {
        public class ValidatedRootPersisted { public uint Index; }
        public class BlockPersisted { public uint Index; }
        private class Timer { public uint Index; }
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private readonly Dictionary<uint, ICancelable> timers = new Dictionary<uint, ICancelable>();
        private readonly MPTPlugin system;
        private readonly ValidationContext context;

        public ValidationService(MPTPlugin system, Wallet wallet)
        {
            this.system = system;
            context = new ValidationContext(wallet);
        }

        private void NewTimer(uint index)
        {
            var timer_token = Context.System.Scheduler.ScheduleTellOnceCancelable(Timeout, Self, new Timer
            {
                Index = index,
            }, ActorRefs.NoSender);
            timers.Add(index, timer_token);
        }

        private void CloseTimer(uint index)
        {
            var need_stop = timers.Keys.Where(p => p <= index).ToArray();
            foreach (var i in need_stop)
                timers.Remove(i);
        }

        private async void SendVote(uint index)
        {
            var vote = context.CreateVote(index);
            if (vote is null) return;
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"relay vote");
            foreach (var url in Settings.Default.Validators)
            {
                try
                {
                    var client = new RpcClient(url);
                    await client?.RpcSendAsync("votestateroot", vote.RootIndex, vote.ValidatorIndex, vote.Signature.ToHexString());
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ValidationService), LogLevel.Warning, $"Failed to send vote, validator={url}, error={e.Message}");
                }
            }
            CheckVotes(index);
        }

        private void OnVoteStateRoot(Vote v)
        {
            if (context.OnVote(v))
                CheckVotes(v.RootIndex);
        }

        private void CheckVotes(uint index)
        {
            var state_root = context.CheckVotes(index);
            if (state_root is null) return;
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"relay state root, height={state_root.Index}, root={state_root.RootHash}");
            system.Store.Tell(state_root);
        }

        private void OnBlockPersisted(uint index)
        {
            if (context.NewProcess(index))
            {
                NewTimer(index);
                SendVote(index);
            }
        }

        private void OnValidatedRootPersisted(uint index)
        {
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"persisted state root, height={index}");
            CloseTimer(index);
            context.OnStateRoot(index);
        }

        private void OnTimer(uint index)
        {
            SendVote(index);
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

        public static Props Props(MPTPlugin system, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new ValidationService(system, wallet));
        }
    }
}

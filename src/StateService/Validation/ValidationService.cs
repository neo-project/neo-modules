using Akka.Actor;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Plugins.StateService.Storage;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.StateService.Validation
{
    public class ValidationService : UntypedActor
    {
        public class ValidatedRootPersisted { public uint Index; }
        public class BlockPersisted { public uint Index; }
        private class Timer { public uint Index; }
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private readonly NeoSystem core;
        private readonly ValidationContext context;

        public ValidationService(NeoSystem core, Wallet wallet)
        {
            this.core = core;
            context = new ValidationContext(wallet);
        }

        private ICancelable NewTimer(uint index)
        {
            return Context.System.Scheduler.ScheduleTellOnceCancelable(Timeout, Self, new Timer
            {
                Index = index,
            }, ActorRefs.NoSender);
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
            var message = context.CheckVotes(index);
            if (message is null) return;
            var state_root = message.Data.AsSerializable<StateRoot>();
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"relay state root, height={state_root.Index}, root={state_root.RootHash}");
            core.Blockchain.Tell(message);
        }

        private void OnBlockPersisted(uint index)
        {
            var p = context.NewProcess(index);
            if (p != null)
            {
                p.Timer = NewTimer(index);
                SendVote(index);
            }
        }

        private void OnValidatedRootPersisted(uint index)
        {
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"persisted state root, height={index}");
            context.StopProcess(index);
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

        public static Props Props(NeoSystem core, Wallet wallet)
        {
            return Akka.Actor.Props.Create(() => new ValidationService(core, wallet));
        }
    }
}

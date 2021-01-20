using Akka.Actor;
using Akka.Util.Internal;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System.Collections.Concurrent;
using System.Linq;

namespace Neo.Plugins.StateService.Verification
{
    public class VerificationContext
    {
        private const int MaxCachedVerificationProcessCount = 10;
        private readonly Wallet wallet;
        private readonly ConcurrentDictionary<uint, VerificationProcess> processes = new ConcurrentDictionary<uint, VerificationProcess>();

        public VerificationContext(Wallet wallet)
        {
            this.wallet = wallet;
        }

        public VerificationProcess NewProcess(uint index)
        {
            if (MaxCachedVerificationProcessCount <= processes.Count)
            {
                var indexes = processes.Keys.OrderBy(i => i).ToArray();
                while (MaxCachedVerificationProcessCount <= indexes.Length)
                {
                    if (processes.TryRemove(indexes[0], out var value))
                    {
                        value.Timer.CancelIfNotNull();
                        indexes = indexes[..1];
                    }
                }
            }
            var p = new VerificationProcess(wallet, index);
            if (!p.IsValidator) return null;
            if (processes.TryAdd(index, p))
            {
                Utility.Log(nameof(VerificationContext), LogLevel.Info, $"new validate process, height={index}, index={p.MyIndex}, ongoing={processes.Count}");
                return p;
            }
            return null;
        }

        public Vote CreateVote(uint index)
        {
            if (processes.TryGetValue(index, out VerificationProcess p))
            {
                return p.CreateVote();
            }
            return null;
        }

        public bool OnVote(Vote vote)
        {
            if (processes.TryGetValue(vote.RootIndex, out VerificationProcess p))
            {
                return p.AddSignature(vote.ValidatorIndex, vote.Signature);
            }
            return false;
        }

        public ExtensiblePayload CheckVotes(uint index)
        {
            if (processes.TryGetValue(index, out VerificationProcess p) && p.CheckSignatures())
            {
                return p.Message;
            }
            return null;
        }

        public void StopProcess(uint index)
        {
            processes.Where(i => i.Key <= index).ForEach(i =>
            {
                if (processes.TryRemove(i.Key, out var value))
                {
                    value.Timer.CancelIfNotNull();
                }
            });
        }
    }
}

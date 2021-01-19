using Akka.Actor;
using Akka.Util.Internal;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System.Collections.Concurrent;
using System.Linq;

namespace Neo.Plugins.StateService.Validation
{
    public class ValidationContext
    {
        const int MaxCachedValidationProcess = 10;
        private readonly Wallet wallet;
        private readonly ConcurrentDictionary<uint, ValidationProcess> processes = new ConcurrentDictionary<uint, ValidationProcess>();

        public ValidationContext(Wallet wallet)
        {
            this.wallet = wallet;
        }

        public ValidationProcess NewProcess(uint index)
        {
            if (MaxCachedValidationProcess <= processes.Count)
            {
                var indexes = processes.Keys.OrderBy(i => i).ToArray();
                while (MaxCachedValidationProcess <= indexes.Length)
                {
                    processes[indexes[0]].Timer.CancelIfNotNull();
                    if (processes.TryRemove(indexes[0], out var value))
                    {
                        indexes = indexes[..1];
                    }
                }
            }
            var p = new ValidationProcess(wallet, index);
            if (!p.IsValidator) return null;
            if (processes.TryAdd(index, p))
            {
                Utility.Log(nameof(ValidationService), LogLevel.Info, $"new validate process, height={index}, index={p.MyIndex}, ongoing={processes.Count}");
                return p;
            }
            return null;
        }

        public Vote CreateVote(uint index)
        {
            if (processes.TryGetValue(index, out ValidationProcess p))
            {
                return p.CreateVote();
            }
            return null;
        }

        public bool OnVote(Vote vote)
        {
            if (processes.TryGetValue(vote.RootIndex, out ValidationProcess p))
            {
                return p.AddSignature(vote.ValidatorIndex, vote.Signature);
            }
            return false;
        }

        public ExtensiblePayload CheckVotes(uint index)
        {
            if (processes.TryGetValue(index, out ValidationProcess p) && p.CheckSignatures())
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

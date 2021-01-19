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
        private readonly ConcurrentDictionary<uint, ValidationProcess> Processes = new ConcurrentDictionary<uint, ValidationProcess>();

        public ValidationContext(Wallet wallet)
        {
            this.wallet = wallet;
        }

        public ValidationProcess NewProcess(uint index)
        {
            if (MaxCachedValidationProcess <= Processes.Count)
            {
                var indexes = Processes.Keys.OrderBy(i => i).ToArray();
                while (MaxCachedValidationProcess <= indexes.Length)
                {
                    Processes[indexes[0]].Timer.CancelIfNotNull();
                    if (Processes.TryRemove(indexes[0], out var value))
                    {
                        indexes = indexes[..1];
                    }
                }
            }
            var p = new ValidationProcess(wallet, index);
            if (!p.IsValidator) return null;
            if (Processes.TryAdd(index, p))
            {
                Utility.Log(nameof(ValidationService), LogLevel.Info, $"new validate process, height={index}, index={p.MyIndex}, ongoing={Processes.Count}");
                return p;
            }
            return null;
        }

        public Vote CreateVote(uint index)
        {
            if (Processes.TryGetValue(index, out ValidationProcess p))
            {
                return p.CreateVote();
            }
            return null;
        }

        public bool OnVote(Vote vote)
        {
            if (Processes.TryGetValue(vote.RootIndex, out ValidationProcess p))
            {
                return p.AddSignature(vote.ValidatorIndex, vote.Signature);
            }
            return false;
        }

        public ExtensiblePayload CheckVotes(uint index)
        {
            if (Processes.TryGetValue(index, out ValidationProcess p) && p.CheckSignatures())
            {
                return p.Message;
            }
            return null;
        }

        public void StopProcess(uint index)
        {
            Processes.Where(i => i.Key <= index).ForEach(i =>
            {
                if (Processes.TryRemove(i.Key, out var value))
                {
                    value.Timer.CancelIfNotNull();
                }
            });
        }
    }
}

using Akka.Actor;
using Akka.Util.Internal;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Neo.Plugins.StateService.Validation
{
    public class ValidationContext
    {
        const int MaxCachedValidationProcess = 10;
        private readonly Wallet wallet;
        private readonly Dictionary<uint, ValidationProcess> Processes = new Dictionary<uint, ValidationProcess>();

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
                    Processes.Remove(indexes[0]);
                    indexes = indexes[..1];
                }
            }
            var p = new ValidationProcess(wallet, index);
            if (!p.IsValidator) return null;
            Processes.Add(index, p);
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"new validate process, height={index}, index={p.MyIndex}, ongoing={Processes.Count}");
            return p;
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
                i.Value.Timer.CancelIfNotNull();
                Processes.Remove(i.Key);
            });
        }
    }
}

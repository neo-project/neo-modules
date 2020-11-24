using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.StateService.Validation
{
    public class ValidationContext
    {
        const int MaxCachedVerificationProcess = 10;
        private readonly Wallet wallet;
        private readonly Dictionary<uint, ValidationProcess> Processes = new Dictionary<uint, ValidationProcess>();

        public ValidationContext(Wallet wallet)
        {
            this.wallet = wallet;
        }

        public bool NewProcess(uint index)
        {
            if (MaxCachedVerificationProcess <= Processes.Count)
            {
                var indexes = Processes.Keys.OrderBy(i => i).ToArray();
                while (MaxCachedVerificationProcess <= indexes.Length)
                {
                    Processes.Remove(indexes[0]);
                    indexes = indexes[..1];
                }
            }
            var p = new ValidationProcess(wallet, index);
            if (!p.IsValidator) return false;
            Processes.Add(index, p);
            Utility.Log(nameof(ValidationService), LogLevel.Info, $"new validate process, height={index}, index={p.MyIndex}, ongoing={Processes.Count}");
            return true;
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

        public StateRoot CheckVotes(uint index)
        {
            if (Processes.TryGetValue(index, out ValidationProcess p))
            {
                if (p.CheckSignatures())
                {
                    return p.StateRoot;
                }
            }
            return null;
        }

        public void OnStateRoot(uint index)
        {
            var indexes = Processes.Keys.Where(i => i <= index).ToList();
            indexes.ForEach(i => Processes.Remove(i));
        }
    }
}

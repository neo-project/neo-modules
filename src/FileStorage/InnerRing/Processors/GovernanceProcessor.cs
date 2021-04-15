using Akka.Actor;
using Neo.Cryptography.ECC;
using Neo.FileStorage.Morph.Event;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class GovernanceProcessor : BaseProcessor
    {
        public override string Name => "GovernanceProcessor";

        public void HandleAlphabetSync(IContractEvent morphEvent)
        {
            Utility.Log(Name, LogLevel.Info, "new event,type:sync");
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessAlphabetSync()) });
        }

        public void ProcessAlphabetSync()
        {
            if (!ActiveState.IsActive())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore alphabet sync");
                return;
            }
            ECPoint[] mainnetAlphabet = MainCli.NeoFSAlphabetList();
            ECPoint[] sidechainAlphabet = MorphCli.Committee();
            ECPoint[] newAlphabet = NewAlphabetList(sidechainAlphabet, mainnetAlphabet);
            if (newAlphabet is null)
            {
                Utility.Log(Name, LogLevel.Info, "no governance update, alphabet list has not been changed");
                return;
            }
            Utility.Log(Name, LogLevel.Info, "alphabet list has been changed, starting update");
            Array.Sort(newAlphabet);
            voter.VoteForSidechainValidator(newAlphabet);
            ECPoint[] innerRing = MorphCli.NeoFSAlphabetList();
            ECPoint[] newInnerRing = UpdateInnerRing(innerRing, sidechainAlphabet, newAlphabet);
            Array.Sort(newInnerRing);
            //todo
        }

        private ECPoint[] NewAlphabetList(ECPoint[] sidechain, ECPoint[] mainnet)
        {
            var ln = sidechain.Length;
            if (ln == 0) throw new Exception("sidechain list is empty");
            if (mainnet.Length < sidechain.Length) throw new Exception(string.Format("expecting {0} keys", ln));
            var hmap = new Dictionary<string, bool>();
            var result = new List<ECPoint>();
            foreach (var node in sidechain) hmap.Add(node.EncodePoint(true).ToScriptHash().ToAddress(ProtocolSettings.AddressVersion), false);
            var newNodes = 0;
            var newNodeLimit = (ln - 1) / 3;
            for (int i = 0; i < ln; i++)
            {
                if (newNodes == newNodeLimit) break;
                var mainnetAddr = mainnet[i].EncodePoint(true).ToScriptHash().ToAddress(ProtocolSettings.AddressVersion);
                if (hmap.TryGetValue(mainnetAddr, out _)) newNodes++;
                else hmap.Add(mainnetAddr, true);
                result.Add(mainnet[i]);
            }
            if (newNodes == 0) return null;
            foreach (var node in sidechain)
            {
                if (result.Count == ln) break;
                if (!hmap[node.EncodePoint(true).ToScriptHash().ToAddress(ProtocolSettings.AddressVersion)]) result.Add(node);
            }
            result.Sort();
            return result.ToArray();
        }

        private ECPoint[] UpdateInnerRing(ECPoint[] innerRing, ECPoint[] before, ECPoint[] after)
        {
            if (before.Length != after.Length) throw new Exception("old and new alphabet lists have different length");
            var result = new List<ECPoint>();
            for (int i = 0; i < innerRing.Length; i++)
            {
                bool loopFlag = false;
                for (int j = 0; j < before.Length; j++)
                {
                    if (innerRing[i].Equals(before[j]))
                    {
                        result.Add(after[j]);
                        loopFlag = true;
                    }
                    if (loopFlag) break;
                }
                if (loopFlag) break;
                result.Add(innerRing[i]);
            }
            return result.ToArray();
        }
    }
}

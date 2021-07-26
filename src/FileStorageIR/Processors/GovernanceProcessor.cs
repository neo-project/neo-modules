using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.Cryptography.ECC;
using Neo.FileStorage.InnerRing.Events;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using Neo.SmartContract.Native;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class GovernanceProcessor : BaseProcessor
    {
        private const string AlphabetUpdateIDPrefix = "AlphabetUpdate";
        public override string Name => "GovernanceProcessor";
        private const string DesignationNotification = "Designation";

        public override HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo designateHandler = new();
            designateHandler.ScriptHashWithType = new ScriptHashWithType() { Type = DesignationNotification, ScriptHashValue = NativeContract.RoleManagement.Hash };
            designateHandler.Handler = HandleAlphabetSync;
            return new HandlerInfo[] { designateHandler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ParserInfo designateParser = new();
            designateParser.ScriptHashWithType = new ScriptHashWithType() { Type = DesignationNotification, ScriptHashValue = NativeContract.RoleManagement.Hash };
            designateParser.Parser = DesignateEvent.ParseDesignateEvent;
            return new ParserInfo[] { designateParser };
        }

        public void HandleAlphabetSync(ContractEvent morphEvent)
        {
            string type;
            if (morphEvent is SyncEvent)
                type = "sync";
            else if (morphEvent is DesignateEvent designateEvent)
            {
                if (designateEvent.Role != (byte)Role.NeoFSAlphabetNode) return;
                type = "designation";
            }
            else
                return;
            Utility.Log(Name, LogLevel.Info, $"event, type={type}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessAlphabetSync()) });
        }

        public void ProcessAlphabetSync()
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore alphabet sync");
                return;
            }
            ECPoint[] mainnetAlphabet;
            try
            {
                mainnetAlphabet = MainInvoker.NeoFSAlphabetList();
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't fetch alphabet list from main net, error={e}");
                return;
            }
            ECPoint[] sidechainAlphabet;
            try
            {
                sidechainAlphabet = MorphInvoker.Committee();
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't fetch alphabet list from side chain, error={e}");
                return;
            }
            ECPoint[] newAlphabet;
            try
            {
                newAlphabet = NewAlphabetList(sidechainAlphabet, mainnetAlphabet);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't merge alphabet lists from main net and side chain, error={e}");
                return;
            }
            if (newAlphabet is null)
            {
                Utility.Log(Name, LogLevel.Info, "no governance update, alphabet list has not been changed");
                return;
            }
            Utility.Log(Name, LogLevel.Info, "alphabet list has been changed, starting update");
            Array.Sort(newAlphabet);
            try
            {
                State.VoteForSidechainValidator(newAlphabet);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, $"can't vote for side chain committee, error={e}");
                return;
            }
            ECPoint[] innerRing;
            try
            {
                innerRing = MorphInvoker.NeoFSAlphabetList();
                ECPoint[] newInnerRing;
                try
                {
                    newInnerRing = UpdateInnerRing(innerRing, sidechainAlphabet, newAlphabet);
                    Array.Sort(newInnerRing);
                    MorphInvoker.SetInnerRing(newInnerRing);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Info, $"can't create new inner ring list with new alphabet keysn, error={e}");
                }
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, $"can't fetch inner ring list from side chain, error={e}");
                return;
            }
            var epoch = State.EpochCounter();
            var id = Utility.StrictUTF8.GetBytes(AlphabetUpdateIDPrefix).Concat(BitConverter.GetBytes(epoch)).ToArray();
            try
            {
                MainInvoker.AlphabetUpdate(id, newAlphabet);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Info, $"can't update list of alphabet nodes in neofs contract, error={e}");
            }
            Utility.Log(Name, LogLevel.Info, "finished alphabet list update");
        }

        public ECPoint[] NewAlphabetList(ECPoint[] sidechain, ECPoint[] mainnet)
        {
            var ln = sidechain.Length;
            if (ln == 0) throw new InvalidOperationException("sidechain list is empty");
            if (mainnet.Length < ln) throw new InvalidOperationException($"alphabet list in mainnet is too short,expecting {ln} keys");
            var hmap = new Dictionary<string, bool>();
            var result = new List<ECPoint>();
            foreach (var node in sidechain) hmap[node.ToAddress(ProtocolSettings.AddressVersion)] = false;
            var newNodes = 0;
            var newNodeLimit = (ln - 1) / 3;
            for (int i = 0; i < mainnet.Length; i++)
            {
                if (result.Count == ln) break;
                var mainnetAddr = mainnet[i].ToAddress(ProtocolSettings.AddressVersion);
                if (!hmap.TryGetValue(mainnetAddr, out _))
                {
                    if (newNodes == newNodeLimit) continue;
                    newNodes++;
                }
                else hmap[mainnetAddr] = true;
                result.Add(mainnet[i]);
            }
            if (newNodes == 0) return null;
            foreach (var node in sidechain)
            {
                if (result.Count == ln) break;
                if (!hmap[node.ToAddress(ProtocolSettings.AddressVersion)]) result.Add(node);
            }
            result.Sort();
            return result.ToArray();
        }

        private ECPoint[] UpdateInnerRing(ECPoint[] innerRing, ECPoint[] before, ECPoint[] after)
        {
            if (before.Length != after.Length) throw new InvalidOperationException("old and new alphabet lists have different length");
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
                if (loopFlag) continue;
                result.Add(innerRing[i]);
            }
            return result.ToArray();
        }
    }
}

using Akka.Actor;
using Neo.Cryptography.ECC;
using Neo.FileStorage.InnerRing.Invoke;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoke;
using Neo.Plugins.util;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.FileStorage.API.Netmap;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Neo.Plugins.util.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AlphabetContractProcessor : IProcessor
    {
        private string name = "AlphabetContractProcessor";

        public IClient Client;
        public IActorRef WorkPool;
        public IIndexer Indexer;
        public ulong StorageEmission;
        public string Name { get => name; set => name = value; }

        public HandlerInfo[] ListenerHandlers()
        {
            return new HandlerInfo[] { };
        }

        public ParserInfo[] ListenerParsers()
        {
            return new ParserInfo[] { };
        }

        public HandlerInfo[] TimersHandlers()
        {
            ScriptHashWithType scriptHashWithType = new ScriptHashWithType()
            {
                Type = Timer.Timers.AlphabetTimer,
            };
            HandlerInfo handler = new HandlerInfo()
            {
                ScriptHashWithType = scriptHashWithType,
                Handler = HandleGasEmission
            };
            return new HandlerInfo[] { handler };
        }

        public void HandleGasEmission(IContractEvent morphEvent)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("tick", ":");
            pairs.Add("type", "alphabet gas emit");
            Neo.Utility.Log(Name, LogLevel.Info, pairs.ParseToString());
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessEmit()) });
        }

        public void ProcessEmit()
        {
            int index = Indexer.Index();
            if (index < 0)
            {
                Neo.Utility.Log(Name, LogLevel.Info, "passive mode, ignore gas emission event");
                return;
            }
            else if (index >= Settings.Default.AlphabetContractHash.Length)
            {
                Neo.Utility.Log(Name, LogLevel.Debug, string.Format("node is out of alphabet range, ignore gas emission event,index:{0}", index.ToString()));
            }
            try
            {
                ContractInvoker.AlphabetEmit(Client, index);
            }
            catch (Exception)
            {
                Neo.Utility.Log(Name, LogLevel.Warning, "can't invoke alphabet emit method");
                return;
            }
            if (StorageEmission == 0)
            {
                Neo.Utility.Log(Name, LogLevel.Info, "storage node emission is off");
                return;
            }
            NodeInfo[] networkMap = null;
            try
            {
                networkMap = ContractInvoker.NetmapSnapshot(Client);
            }
            catch (Exception e)
            {
                Neo.Utility.Log(Name, LogLevel.Warning, string.Format("can't get netmap snapshot to emit gas to storage nodes,{0}", e.Message));
                return;
            }
            if (networkMap.Length == 0)
            {
                Neo.Utility.Log(Name, LogLevel.Debug, "empty network map, do not emit gas");
                return;
            }
            var gasPerNode = (long)StorageEmission * 100000000 / networkMap.Length;
            for (int i = 0; i < networkMap.Length; i++)
            {
                ECPoint key = null;
                try
                {
                    key = ECPoint.FromBytes(networkMap[i].PublicKey.ToByteArray(), ECCurve.Secp256r1);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log(Name, LogLevel.Warning, string.Format("can't convert node public key to address,{0}", e.Message));
                    continue;
                }
                try
                {
                    ((MorphClient)Client).TransferGas(key.EncodePoint(true).ToScriptHash(), gasPerNode);
                }
                catch (Exception e)
                {
                    Dictionary<string, string> pairs = new Dictionary<string, string>();
                    pairs.Add("can't transfer gas", ":");
                    pairs.Add("receiver", e.Message);
                    pairs.Add("amount", key.EncodePoint(true).ToScriptHash().ToAddress());
                    Neo.Utility.Log(Name, LogLevel.Warning, pairs.ParseToString());
                }
            }
        }
    }
}

using Akka.Actor;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.Morph.Event;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Threading.Tasks;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AlphabetContractProcessor : BaseProcessor
    {
        public override string Name => "AlphabetContractProcessor";
        public ulong StorageEmission;

        public override HandlerInfo[] TimersHandlers()
        {
            ScriptHashWithType scriptHashWithType = new ScriptHashWithType()
            {
                Type = Timers.AlphabetTimer,
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
            Utility.Log(Name, LogLevel.Info, string.Format("tick,type:alphabet gas emit"));
            WorkPool.Tell(new NewTask() { process = Name, task = new Task(() => ProcessEmit()) });
        }

        public void ProcessEmit()
        {
            int index = Indexer.Index();
            if (index < 0)
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore gas emission event");
                return;
            }
            else if (index >= Settings.Default.AlphabetContractHash.Length)
            {
                Utility.Log(Name, LogLevel.Debug, string.Format("node is out of alphabet range, ignore gas emission event,index:{0}", index.ToString()));
            }
            try
            {
                ContractInvoker.AlphabetEmit(MorphCli, index);
            }
            catch (Exception)
            {
                Utility.Log(Name, LogLevel.Warning, "can't invoke alphabet emit method");
                return;
            }
            if (StorageEmission == 0)
            {
                Utility.Log(Name, LogLevel.Info, "storage node emission is off");
                return;
            }
            NodeInfo[] networkMap = null;
            try
            {
                networkMap = ContractInvoker.NetmapSnapshot(MorphCli);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Warning, string.Format("can't get netmap snapshot to emit gas to storage nodes,error:{0}", e.Message));
                return;
            }
            if (networkMap.Length == 0)
            {
                Utility.Log(Name, LogLevel.Debug, "empty network map, do not emit gas");
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
                    Utility.Log(Name, LogLevel.Warning, string.Format("can't convert node public key to address,error:{0}", e.Message));
                    continue;
                }
                try
                {
                    MorphCli.TransferGas(key.EncodePoint(true).ToScriptHash(), gasPerNode);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, string.Format("can't transfer gas,receiver:{0},amount:{1},error:{2}", key.EncodePoint(true).ToScriptHash().ToAddress(ProtocolSettings.AddressVersion), gasPerNode, e.Message));
                }
            }
        }
    }
}

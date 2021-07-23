using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Cryptography;
using Neo.Wallets;
using static Neo.FileStorage.Utils.WorkerPool;
using static Neo.Wallets.Helper;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class AlphabetContractProcessor : BaseProcessor
    {
        public override string Name => "AlphabetContractProcessor";
        public ulong StorageEmission => Settings.Default.StorageEmission;

        public void HandleGasEmission()
        {
            Utility.Log(Name, LogLevel.Info, "event, type=AlphabetGasEmit");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessEmit()) });
        }

        public void ProcessEmit()
        {
            int index = State.AlphabetIndex();
            if (index < 0)
            {
                Utility.Log(Name, LogLevel.Debug, $"non alphabet mode, ignore gas emission event, index={index}");
                return;
            }
            else if (index >= Settings.Default.AlphabetContractHash.Length)
            {
                Utility.Log(Name, LogLevel.Debug, $"node is out of alphabet range, ignore gas emission event, index={index}");
            }
            try
            {
                MorphInvoker.AlphabetEmit(index);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Warning, $"can't invoke alphabet emit method, error={e}");
                return;
            }
            if (StorageEmission == 0)
            {
                Utility.Log(Name, LogLevel.Info, "storage node emission is off");
                return;
            }
            API.Netmap.NodeInfo[] networkMap;
            try
            {
                networkMap = MorphInvoker.NetMap();
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Warning, $"can't get netmap snapshot to emit gas to storage nodes,error={e}");
                return;
            }
            if (!networkMap.Any())
            {
                Utility.Log(Name, LogLevel.Debug, "empty network map, do not emit gas");
                return;
            }
            var gasPerNode = (long)StorageEmission * 100000000 / networkMap.Length;
            for (int i = 0; i < networkMap.Length; i++)
            {
                ECPoint key;
                try
                {
                    key = ECPoint.FromBytes(networkMap[i].PublicKey.ToByteArray(), ECCurve.Secp256r1);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"can't convert node public key to address, error={e}");
                    continue;
                }
                UInt160 scriptHash = key.ToScriptHash();
                try
                {
                    MorphInvoker.TransferGas(scriptHash, gasPerNode);
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Warning, $"can't transfer gas, receiver={scriptHash.ToAddress(ProtocolSettings.AddressVersion)}, amount={gasPerNode}, error={e}");
                }
            }
        }
    }
}

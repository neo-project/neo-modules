using Neo.Plugins;
using Neo.ConsoleService;
using System;
using Neo.Wallets;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using System.Threading;
using Neo.FileStorage.API.Refs;
using Neo.SmartContract.Native;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using Neo.SmartContract;
using Akka.Actor;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        private static UInt160 FsContractHash => Settings.Default.fsContractHash;

        [ConsoleCommand("fs account balance", Category = "FileStorageService", Description = "Show account balance")]
        private void OnAccountBalance(string paccount)
        {
            if (NoWallet()) return;
            var account = currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash;
            var key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            var pk = Neo.Cryptography.ECC.ECPoint.Parse(paccount, Neo.Cryptography.ECC.ECCurve.Secp256r1);
            var ownerID = OwnerID.FromScriptHash(pk.EncodePoint(true).PublicKeyToScriptHash());
            using var client = new Client(key, Host);
            if (OnGetBalanceInternal(client, key, out Neo.FileStorage.API.Accounting.Decimal result))
                Console.WriteLine($"Fs current account :{Contract.CreateSignatureRedeemScript(pk).ToScriptHash()}, balance:{(result.Value == 0 ? 0 : result)}");
        }

        [ConsoleCommand("fs account withdraw", Category = "FileStorageService", Description = "Withdraw account balance")]
        private void OnAccountWithdraw(string pamount, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out UInt160 account, out ECDsa key)) return;
            var amount = int.Parse(pamount);
            if (amount <= 0)
            {
                Console.WriteLine("Amount cannot be negative");
                return;
            }
            using SnapshotCache snapshot = System.GetSnapshot();
            using var client = new Client(key, Host);
            if (!OnGetBalanceInternal(client, key, out Neo.FileStorage.API.Accounting.Decimal balance)) return;
            if (balance.Value < amount * NativeContract.GAS.Decimals)
            {
                Console.WriteLine($"Fs current account balance is not enough");
                return;
            }
            byte[] script = FsContractHash.MakeScript("withdraw", account, amount);
            Transaction tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = script,
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + System.Settings.MaxValidUntilBlockIncrement,
                Signers = new Signer[] { new Signer() { Account = account, Scopes = WitnessScope.Global } },
                Attributes = Array.Empty<TransactionAttribute>(),
            };
            var data = new ContractParametersContext(snapshot, tx, System.Settings.Network);
            currentWallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, tx, null, System.Settings);
            if (engine.State != VMState.HALT)
            {
                Console.WriteLine($"Execution of Withdraw request failed,error:{engine.FaultException}");
            }
            tx.SystemFee = engine.GasConsumed;
            tx.NetworkFee = currentWallet.CalculateNetworkFee(snapshot, tx);
            if (NativeContract.GAS.BalanceOf(snapshot, account) < engine.GasConsumed + tx.NetworkFee)
            {
                Console.WriteLine("Gas insufficient");
                return;
            }
            data = new ContractParametersContext(snapshot, tx, System.Settings.Network);
            currentWallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            System.Blockchain.Tell(tx);
            Console.WriteLine($"The withdraw request has been submitted, please confirm in the next block,TxID:{tx.Hash}");
        }

        [ConsoleCommand("fs account deposite", Category = "FileStorageService", Description = "Deposite account balance")]
        private void OnAccountDeposite(string pamount, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out UInt160 account, out _)) return;
            using SnapshotCache snapshot = System.GetSnapshot();
            AssetDescriptor descriptor = new AssetDescriptor(snapshot, System.Settings, NativeContract.GAS.Hash);
            if (!BigDecimal.TryParse(pamount, descriptor.Decimals, out BigDecimal decimalAmount) || decimalAmount.Sign <= 0)
            {
                Console.WriteLine("Incorrect Amount Format");
                return;
            }

            if (NativeContract.GAS.BalanceOf(snapshot, account) < decimalAmount.Value)
            {
                Console.WriteLine("Gas insufficient");
                return;
            }
            var FsContractHash = Settings.Default.fsContractHash;
            byte[] script = NativeContract.GAS.Hash.MakeScript("transfer", account, FsContractHash, decimalAmount.Value, Array.Empty<byte>());
            Transaction tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = script,
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + System.Settings.MaxValidUntilBlockIncrement,
                Signers = new Signer[] { new Signer() { Account = account, Scopes = WitnessScope.Global } },
                Attributes = Array.Empty<TransactionAttribute>(),
            };
            var data = new ContractParametersContext(snapshot, tx, System.Settings.Network);
            currentWallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, tx, null, System.Settings);
            if (engine.State != VMState.HALT)
            {
                Console.WriteLine($"Execution of Withdraw request failed,error:{engine.FaultException}");
                return;
            }
            tx.SystemFee = engine.GasConsumed;
            tx.NetworkFee = currentWallet.CalculateNetworkFee(snapshot, tx);
            if (NativeContract.GAS.BalanceOf(snapshot, account) < engine.GasConsumed + tx.NetworkFee)
            {
                Console.WriteLine("Gas insufficient");
                return;
            }
            data = new ContractParametersContext(snapshot, tx, System.Settings.Network);
            currentWallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            System.Blockchain.Tell(tx);
            Console.WriteLine($"The deposite request has been submitted, please confirm in the next block,TxID:{tx.Hash}");
        }

        private bool OnGetBalanceInternal(Client client, ECDsa key, out Neo.FileStorage.API.Accounting.Decimal result)
        {
            OwnerID ownerID = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash());
            using var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            try
            {
                result = client.GetBalance(ownerID, context: source.Token).Result;
                source.Cancel();
                return result is not null;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs get account balance fail,error:{e}");
                result = null;
                source.Cancel();
                return false;
            }
        }
    }
}

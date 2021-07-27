using Neo.Plugins;
using Neo.ConsoleService;
using System;
using Neo.Wallets;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Acl;
using System.Threading;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;
using Google.Protobuf;
using System.Text;
using Neo.SmartContract.Native;
using Neo.Network.P2P.Payloads;
using System.Globalization;
using Neo.Persistence;
using Neo.VM;
using Neo.SmartContract;
using Akka.Actor;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs account balance", Category = "FileStorageService", Description = "Show account balance")]
        private void OnAccountBalance(string paccount)
        {
            if (NoWallet()) return;
            UInt160 account = currentWallet.GetAccounts().Where(p => !p.WatchOnly)?.ToArray()[0].ScriptHash;
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var source = new CancellationTokenSource();
                source.CancelAfter(10000);
                Neo.Cryptography.ECC.ECPoint pk = Neo.Cryptography.ECC.ECPoint.Parse(paccount, Neo.Cryptography.ECC.ECCurve.Secp256r1);
                OwnerID ownerID = pk.EncodePoint(true).PublicKeyToOwnerID();
                Neo.FileStorage.API.Accounting.Decimal result = client.GetBalance(ownerID, context: source.Token).Result;
                Console.WriteLine($"Fs current account :{Contract.CreateSignatureRedeemScript(pk).ToScriptHash()}, balance:{(result.Value == 0 ? 0 : result)}");
            }
        }

        [ConsoleCommand("fs account withdraw", Category = "FileStorageService", Description = "Withdraw account balance")]
        private void OnAccountWithdraw(string pamount, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var amount = int.Parse(pamount);
            if (amount <= 0)
            {
                Console.WriteLine("Amount cannot be negative");
                return;
            }
            using (SnapshotCache snapshot = System.GetSnapshot())
            {
                var host = Settings.Default.host;
                ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
                using (var client = new Client(key, host))
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(10000);
                    Neo.FileStorage.API.Accounting.Decimal balance = client.GetBalance(context: source.Token).Result;
                    if (balance.Value < amount * NativeContract.GAS.Decimals)
                    {
                        Console.WriteLine($"Fs current account balance is not enough");
                        return;
                    }
                }
                var FsContractHash = Settings.Default.fsContractHash;
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
                Console.WriteLine($"The Withdraw request has been submitted, please confirm in the next block,TxID:{tx.Hash}");
            }
        }

        [ConsoleCommand("fs account deposite", Category = "FileStorageService", Description = "Deposite account balance")]
        private void OnAccountDeposite(string pamount, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var amount = int.Parse(pamount);
            if (amount < 0)
            {
                Console.WriteLine("Amount cannot be negative");
                return;
            }
            using (SnapshotCache snapshot = System.GetSnapshot())
            {
                if (NativeContract.GAS.BalanceOf(snapshot, account) < amount * NativeContract.GAS.Decimals)
                {
                    Console.WriteLine("Gas insufficient");
                    return;
                }
                var FsContractHash = Settings.Default.fsContractHash;
                byte[] script = NativeContract.GAS.Hash.MakeScript("transfer", account, FsContractHash, amount, new byte[0]);
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
                if (NativeContract.GAS.BalanceOf(snapshot, account) < engine.GasConsumed + tx.NetworkFee + amount * NativeContract.GAS.Decimals)
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
        }
    }
}

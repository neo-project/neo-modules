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
        private static UInt160 FsContractHash => Settings.Default.FsContractHash;

        /// <summary>
        /// User can invoke this command to query the account balance in fs.
        /// </summary>
        /// <param name="paddress">account address</param>
        [ConsoleCommand("fs account balance", Category = "FileStorageService", Description = "Show account balance")]
        private void OnAccountBalance(string paddress)
        {
            if (NoWallet()) return;
            var account = currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash;
            var key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            var ownerID = OwnerID.FromScriptHash(paddress.ToScriptHash(System.Settings.AddressVersion));
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            if (OnGetBalanceInternal(client, key, out Neo.FileStorage.API.Accounting.Decimal result))
                Console.WriteLine($"Fs account :{paddress}, balance:{(result.Value == 0 ? 0 : result)}");
        }

        /// <summary>
        /// User can invoke this command to withdraw asset.
        /// </summary>
        /// <param name="pamount">amount</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs account withdraw", Category = "FileStorageService", Description = "Withdraw account balance")]
        private void OnAccountWithdraw(string pamount, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out UInt160 account, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            if (!OnGetBalanceInternal(client, key, out Neo.FileStorage.API.Accounting.Decimal balance)) return;
            if (!int.TryParse(pamount, out int amount) || amount <= 0) throw new Exception($"Fs withdraw amount can not be negative");
            using SnapshotCache snapshot = System.GetSnapshot();
            if (balance.Value < amount * NativeContract.GAS.Decimals) throw new Exception($"Fs account balance is not enough");
            byte[] script = FsContractHash.MakeScript("withdraw", account, amount);
            if (OnMakeTransactionInternal(script, snapshot, account, out var tx))
                Console.WriteLine($"The withdraw request has been submitted, please confirm in the next block,TxID:{tx.Hash}");
        }

        /// <summary>
        /// User can invoke this command to deposit asset.
        /// </summary>
        /// <param name="pamount">amount</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs account deposit", Category = "FileStorageService", Description = "Deposite account balance")]
        private void OnAccountDeposit(string pamount, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out UInt160 account, out _)) return;
            using SnapshotCache snapshot = System.GetSnapshot();
            AssetDescriptor descriptor = new AssetDescriptor(snapshot, System.Settings, NativeContract.GAS.Hash);
            if (!BigDecimal.TryParse(pamount, descriptor.Decimals, out BigDecimal decimalAmount) || decimalAmount.Sign <= 0) throw new Exception($"Incorrect amount format");
            if (NativeContract.GAS.BalanceOf(snapshot, account) < decimalAmount.Value) throw new Exception($"Fs account balance is not enough");
            byte[] script = NativeContract.GAS.Hash.MakeScript("transfer", account, FsContractHash, decimalAmount.Value, Array.Empty<byte>());
            if (OnMakeTransactionInternal(script, snapshot, account, out var tx))
                Console.WriteLine($"The deposit request has been submitted, please confirm in the next block,TxID:{tx.Hash}");
        }

        //internal function
        private bool OnGetBalanceInternal(Client client, ECDsa key, out Neo.FileStorage.API.Accounting.Decimal result)
        {
            result = null;
            OwnerID ownerID = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash());
            using CancellationTokenSource source = new();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                result = client.GetBalance(ownerID, context: source.Token).Result;
                source.Cancel();
                return result is not null;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs get account balance fault,error:{e}");
                source.Cancel();
                return false;
            }
        }

        private bool OnMakeTransactionInternal(byte[] script, SnapshotCache snapshot, UInt160 account, out Transaction tx)
        {
            tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = script,
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + System.Settings.MaxValidUntilBlockIncrement,
                Signers = new Signer[] { new Signer() { Account = account, Scopes = WitnessScope.Global } },
                Attributes = Array.Empty<TransactionAttribute>(),
            };
            ContractParametersContext data = new(snapshot, tx, System.Settings.Network);
            currentWallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, tx, null, System.Settings);
            if (engine.State != VMState.HALT) throw new Exception($"Fs send tx fault,error:{engine.FaultException}");
            tx.SystemFee = engine.GasConsumed;
            tx.NetworkFee = currentWallet.CalculateNetworkFee(snapshot, tx);
            if (NativeContract.GAS.BalanceOf(snapshot, account) < engine.GasConsumed + tx.NetworkFee) throw new Exception($"Fs account gas is not enough");
            data = new ContractParametersContext(snapshot, tx, System.Settings.Network);
            currentWallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            System.Blockchain.Tell(tx);
            return true;
        }
    }
}

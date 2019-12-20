#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System.Linq;
using System.Numerics;
using static System.IO.Path;
using SystemFile=System.IO.File;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System;

namespace Neo.Plugins
{
    partial class RestController
    {
        private static Wallet wallet;
        private void CheckWallet()
        {
            if (wallet is null)
                throw new RestException(-400, "Access denied");
        }

        /// <summary>
        /// Close the wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("wallets/closewallet")]
        public IActionResult CloseWallet()
        {
            wallet = null;
            return Ok("Success");
        }

        /// <summary>
        /// Exports the private key of the specified address. 
        /// </summary>
        /// <param name="address"> Addresse of the private key, required to be a standard address.</param>
        /// <returns></returns>
        [HttpGet("wallets/dumpprivkey")]
        public IActionResult DumpPrivKey(string address)
        {
            CheckWallet();
            UInt160 scriptHash = address.ToScriptHash();
            WalletAccount account = wallet.GetAccount(scriptHash);
            return Ok(account.GetKey().Export());
        }

        /// <summary>
        /// Balance of the specified asset 
        /// </summary>
        /// <param name="assetID"> Asset id</param>
        /// <returns></returns>
        [HttpGet("wallets/balance")]
        public IActionResult GetBalance(string assetID)
        {
            CheckWallet();
            UInt160 asset_id = UInt160.Parse(assetID);
            JObject json = new JObject();
            json["balance"] = wallet.GetAvailable(asset_id).Value.ToString();
            return FormatJson(json);
        }

        /// <summary>
        /// Create a new address
        /// </summary>
        /// <returns></returns>
        [HttpGet("wallets/newaddress")]
        public IActionResult GetNewAddress()
        {
            CheckWallet();
            WalletAccount account = wallet.CreateAccount();
            if (wallet is NEP6Wallet nep6)  
                nep6.Save();
            return Ok(account.Address);
        }

        /// <summary>
        /// Get the amount of unclaimed GAS
        /// </summary>
        /// <returns></returns>
        [HttpGet("wallets/unclaimedgas")]
        public IActionResult GetUnclaimedGas()
        {
            CheckWallet();
            BigInteger gas = BigInteger.Zero;
            using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                foreach (UInt160 account in wallet.GetAccounts().Select(p => p.ScriptHash))
                {
                    gas += NativeContract.NEO.UnclaimedGas(snapshot, account, snapshot.Height + 1);
                }
            return Ok(gas.ToString());
        }

        /// <summary>
        /// Import the private key 
        /// </summary>
        /// <param name="privkey">The WIF-format private key</param>
        /// <returns></returns>
        [HttpGet("wallets/importprivkey")]
        public IActionResult ImportPrivKey(string privkey)
        {
            CheckWallet();
            WalletAccount account = wallet.Import(privkey);
            if (wallet is NEP6Wallet nep6wallet)
                nep6wallet.Save();
            JObject json =  new JObject
            {
                ["address"] = account.Address,
                ["haskey"] = account.HasKey,
                ["label"] = account.Label,
                ["watchonly"] = account.WatchOnly
            };
            return FormatJson(json);
        }

        /// <summary>
        /// Open the wallet
        /// </summary>
        /// <param name="path"> Path of the wallet</param>
        /// <param name="password">  Wallet password</param>
        /// <returns></returns>
        [HttpPost("wallets/openwallet")]
        public IActionResult OpenWallet(string path, string password)
        {
            if (!SystemFile.Exists(path)) return NotFound();
            switch (GetExtension(path))
            {
                case ".db3":
                    {
                        wallet = UserWallet.Open(path, password);
                        break;
                    }
                case ".json":
                    {
                        NEP6Wallet nep6wallet = new NEP6Wallet(path);
                        nep6wallet.Unlock(password);
                        wallet = nep6wallet;
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }
            return Ok("Success");
        }

        /// <summary>
        /// List all the addresses
        /// </summary>
        /// <returns></returns>
        [HttpGet("wallets/listaddresses")]
        public IActionResult ListAddress()
        {
            CheckWallet();
            JArray array = wallet.GetAccounts().Select(p =>
            {
                JObject account = new JObject();
                account["address"] = p.Address;
                account["haskey"] = p.HasKey;
                account["label"] = p.Label;
                account["watchonly"] = p.WatchOnly;
                return account;
            }).ToArray();
            return FormatJson(array);
        }

        /// <summary>
        /// Transfer from the specified address to the destination address
        /// </summary>
        /// <param name="assetid"> Asset Id</param>
        /// <param name="from"> Source address </param>
        /// <param name="to">  Destination address </param>
        /// <param name="amount"> Transfer amount </param>
        /// <returns></returns>
        [HttpPost("wallets/sendasset")]
        public IActionResult SendFrom(string assetid, string from, string to, string amount)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(assetid);
            UInt160 _from = from.ToScriptHash();
            UInt160 _to = to.ToScriptHash();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal _amount = BigDecimal.Parse(amount, descriptor.Decimals);
            if (_amount.Sign <= 0)
                throw new RestException(-300, "Insufficient funds");
            Transaction tx = wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = _amount,
                    ScriptHash = _to
                }
            }, _from);
            if (tx == null)
                throw new RestException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return FormatJson(transContext.ToJson());
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > Settings.Default.MaxFee)
                throw new RestException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return FormatJson(SignAndRelay(tx));
        }

        /// <summary>
        /// Transfer assets in batch
        /// </summary>
        /// <param name="assets"> Array of assets to be transferred </param>
        /// <returns></returns>
        [HttpPost("wallets/sendmany")]
        public IActionResult SendMany(Assets assets)
        {
            CheckWallet();
            UInt160 from = null;
            if (assets.From != null || assets.From.Length != 0)
            {
                from = assets.From.ToScriptHash();
            }
            List<Asset> to = assets.Asset;
            if (to.Count == 0)
                throw new RestException(-32602, "Invalid params");
            TransferOutput[] outputs = new TransferOutput[to.Count];
            for (int i = 0; i < to.Count; i++)
            {
                UInt160 asset_id = UInt160.Parse(to[i].AssetId);
                AssetDescriptor descriptor = new AssetDescriptor(asset_id);
                outputs[i] = new TransferOutput
                {
                    AssetId = asset_id,
                    Value = BigDecimal.Parse(to[i].Value, descriptor.Decimals),
                    ScriptHash = to[i].Address.ToScriptHash()
                };
                if (outputs[i].Value.Sign <= 0)
                    throw new RestException(-32602, "Invalid params");
            }
            Transaction tx = wallet.MakeTransaction(outputs, from);
            if (tx == null)
                throw new RestException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return FormatJson(transContext.ToJson());
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > Settings.Default.MaxFee)
                throw new RestException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return FormatJson(SignAndRelay(tx));
        }


        /// <summary>
        /// Transfer to the specified address
        /// </summary>
        /// <param name="asset"> Asset information to be transferred </param>
        /// <returns></returns>
        [HttpPost("wallets/sendtoaddress")]
        public IActionResult SendToAddress(Asset asset)
        {
            CheckWallet();
            UInt160 assetId = UInt160.Parse(asset.AssetId);
            UInt160 scriptHash = asset.Address.ToScriptHash();
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal amount = BigDecimal.Parse(asset.Value, descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new RestException(-32602, "Invalid params");
            Transaction tx = wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = scriptHash
                }
            });
            if (tx == null)
                throw new RestException(-300, "Insufficient funds");

            ContractParametersContext transContext = new ContractParametersContext(tx);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return FormatJson(transContext.ToJson());
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            if (tx.NetworkFee > Settings.Default.MaxFee)
                throw new RestException(-301, "The necessary fee is more than the Max_fee, this transaction is failed. Please increase your Max_fee value.");
            return FormatJson(SignAndRelay(tx));
        }

        private JObject SignAndRelay(Transaction tx)
        {
            ContractParametersContext context = new ContractParametersContext(tx);
            wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return tx.ToJson();
            }
            else
            {
                return context.ToJson();
            }
        }
        private void ProcessInvokeWithWallet(JObject result)
        {
            if (wallet != null)
            {
                Transaction tx = wallet.MakeTransaction(result["script"].AsString().HexToBytes());
                ContractParametersContext context = new ContractParametersContext(tx);
                wallet.Sign(context);
                if (context.Completed)
                    tx.Witnesses = context.GetWitnesses();
                else
                    tx = null;
                result["tx"] = tx?.ToArray().ToHexString();
            }
        }
    }
}

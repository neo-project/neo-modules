using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.IO;
using Neo.VM;
using System.Linq;
using Neo.Wallets.NEP6;
using Neo.Ledger;
using System.Collections.Generic;
using NeoFS.API.v2.Netmap;
using Google.Protobuf;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Container;
using Neo.Cryptography;
using System.IO;

namespace Neo.Plugins.FSStorage.morph.client.Tests
{
    [TestClass()]
    public class UT_FSNode
    {
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
        }

        [TestMethod()]
        public void InitTest()
        {
            var sys = TestBlockchain.TheNeoSystem;
            NEP6Wallet temp = TestBlockchain.wallet;
            Console.WriteLine(temp.GetAccounts().ToArray()[0].GetKey().PublicKey.EncodePoint(true).ToHexString());
        }

/*        [TestMethod()]
        public void FsContractInnerRingUpdate()
        {
            NEP6Wallet walletIR = new NEP6Wallet("./walletIR.json");
            walletIR.Unlock("123456");
            for (int i = 1; i <= 7; i++)
            {
                string keyPath = "C:/Users/Shinelon/Desktop/neo-document/NeoFS/测试/0" + i + ".key";
                byte[] privateKey = File.ReadAllBytes(keyPath);
                KeyPair keyPair = new KeyPair(privateKey);
                Console.WriteLine("WIF:" + keyPair.Export());
                walletIR.Import(keyPair.Export());
            }
            //walletIR.Save();


            var wallet1 = walletIR;//TestBlockchain.wallet;
            var accounts = wallet1.GetAccounts();
            var FsContractHash = UInt160.Parse("0x5f490fbd8010fd716754073ee960067d28549b7d");//0xbbf24e35a65a9102443206921e0a2479af7b8f9c
            var script = MakeScript(FsContractHash, "innerRingUpdate",new byte[] { 0x02}, new byte[][] { accounts.ToArray()[0].GetKey().PublicKey.ToArray(), accounts.ToArray()[1].GetKey().PublicKey.ToArray(), accounts.ToArray()[2].GetKey().PublicKey.ToArray(), accounts.ToArray()[3].GetKey().PublicKey.ToArray(), accounts.ToArray()[4].GetKey().PublicKey.ToArray(), accounts.ToArray()[5].GetKey().PublicKey.ToArray(), accounts.ToArray()[6].GetKey().PublicKey.ToArray() });
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 1246,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[6].ScriptHash ,Scopes=WitnessScope.Global} },
                SystemFee = 1000000000,
                ValidUntilBlock = 4000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            //var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, new FakeSigners(accounts.ToArray()[0].ScriptHash), null, 0, tx.SystemFee);
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            //Console.WriteLine("tx:"+engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void BalanceContractBalanceOf()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var BalanceContractHash = UInt160.Parse("0x08953affe65148d7ec4c8db5a0a6977c32ddf54c");
            var script = BalanceContractHash.MakeScript("balanceOf", accounts.ToArray()[0].ScriptHash);
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, null, null, 0, 20000000000);
            Console.WriteLine("tx:" + engine.State);
            Console.WriteLine(Convert.ToBase64String(script.ToArray()));

            Console.WriteLine(script.ToArray().ToHexString());
            //Console.WriteLine(Convert.ToBase64String(script));
        }


        [TestMethod()]
        public void FsContractBind()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var FsContractHash = UInt160.Parse("0xbbf24e35a65a9102443206921e0a2479af7b8f9c");
            var txId = UInt256.Parse("0xce9524e8215d2a6c26271dcccfec58cae563cfdf9ef9287b473a38fcbeef6847");
            var script = MakeScript(FsContractHash,"bind", accounts.ToArray()[0].GetKey().PublicKey.ToArray(), new byte[][] { accounts.ToArray()[0].GetKey().PublicKey.ToArray()});
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 3052,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash ,Scopes=WitnessScope.Global} },
                SystemFee = 2000000000,
                ValidUntilBlock = 4500,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:"+engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void FsContractDeposite()
        {
            var newwallet = new MyWallet("test");
            newwallet.Import("L3o221BojgcCPYgdbXsm6jn7ayTZ72xwREvBHXKknR8VJ3G4WmjB");
            var accounts = newwallet.GetAccounts();
            var FsContractHash = UInt160.Parse("0x5f490fbd8010fd716754073ee960067d28549b7d");//0xbbf24e35a65a9102443206921e0a2479af7b8f9c
            //var script = FsContractHash.MakeScript("innerRingList");
            var script = FsContractHash.MakeScript("deposit", accounts.ToArray()[0].ScriptHash.ToArray(), 50, new byte[0]);
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 3088,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                SystemFee = 2000000000,
                ValidUntilBlock = 5000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            //var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            //Console.WriteLine("tx:" + engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
            //Console.WriteLine(FsContractHash.ToAddress());
            //Console.WriteLine(new Signer() { Account = accounts.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global }.ToJson());
        }

        [TestMethod()]
        public void FsContractWithDraw()
        {
            var newwallet = new MyWallet("test");
            newwallet.Import("L2NpJUsXCm3ajA98bzFWFztjTNrXcfYU9xWzHZgUasvTSA6rnRrR");
            IEnumerable<WalletAccount> accountstemp = newwallet.GetAccounts();
            KeyPair key = accountstemp.ToArray()[0].GetKey();
            Console.WriteLine(key.PublicKeyHash.ToAddress());
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var FsContractHash = Settings.Default.FsContractHash;
            var script = FsContractHash.MakeScript("withdraw", accountstemp.ToArray()[0].ScriptHash.ToArray(), 20);
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce =480,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accountstemp.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                SystemFee = 1000000000,
                ValidUntilBlock = 3000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:" + engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void FsContractDeposit()
        {
            var newwallet = TestBlockchain.wallet;
            //newwallet.Import("Kz4EXUzMWasf2peTUByGqnRoKT5bYTHVhHF4DPnKrHMY8U4FeoJZ");
            IEnumerable<WalletAccount> accountstemp = TestBlockchain.wallet.GetAccounts();
            KeyPair key = accountstemp.ToArray()[0].GetKey();
            Console.WriteLine(key.PublicKeyHash.ToAddress());
            var FsContractHash = Settings.Default.FsContractHash;
            var script = FsContractHash.MakeScript("deposit", accountstemp.ToArray()[0].ScriptHash.ToArray(), 50,new byte[0]);
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 300,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accountstemp.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                SystemFee = 2000000000,
                ValidUntilBlock = 5000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:" + engine.State);
            Console.WriteLine("account:" + accountstemp.ToArray()[0].ScriptHash.ToAddress());
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        private static byte[] MakeScript(UInt160 scriptHash, string operation,byte[] arg0, byte[][] args)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                for (int i = args.Length - 1; i >= 0; i--)
                {
                    sb.EmitPush(args[i]);
                }
                sb.EmitPush(args.Length);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(arg0);
                sb.EmitPush(2);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(operation);
                sb.EmitPush(scriptHash);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
                return sb.ToArray();
            }
        }

        private static byte[] MakeScript(UInt160 scriptHash, string operation, byte[][] args)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                for (int i = args.Length - 1; i >= 0; i--)
                {
                    sb.EmitPush(args[i]);
                }
                sb.EmitPush(args.Length);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(1);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(operation);
                sb.EmitPush(scriptHash);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
                return sb.ToArray();
            }
        }

        [TestMethod()]
        public void BalanceContractInit()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var balanceContractHash = Settings.Default.BalanceContractHash;
            var containerContractHash = Settings.Default.ContainerContractHash;
            var netmapContractHash = Settings.Default.NetmapContractHash;
            var script = balanceContractHash.MakeScript("init", netmapContractHash.ToArray(), containerContractHash.ToArray());
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash } },
                SystemFee = 1000000000,
                ValidUntilBlock = 1000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:");
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void NetMapContractHashContractInit()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var netmapContractHash = Settings.Default.NetmapContractHash;
            var script = MakeScript(netmapContractHash, "init", new byte[][] { accounts.ToArray()[0].GetKey().PublicKey.ToArray(), accounts.ToArray()[1].GetKey().PublicKey.ToArray(), accounts.ToArray()[2].GetKey().PublicKey.ToArray() });
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash } },
                SystemFee = 1000000000,
                ValidUntilBlock = 1000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:");
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void NetMapContractHashContractInitConfig()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var netmapContractHash = Settings.Default.NetmapContractHash;
            var script = MakeScript(netmapContractHash, "initConfig", new byte[][] { Utility.StrictUTF8.GetBytes("ContainerFee"), BitConverter.GetBytes(0) });
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash } },
                SystemFee = 1000000000,
                ValidUntilBlock = 5000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:");
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void ContainerContractHashContractInit()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var containerContractHash = Settings.Default.ContainerContractHash;
            var netMapContractHash = Settings.Default.NetmapContractHash;
            var balanceContractHash = Settings.Default.BalanceContractHash;
            var neofsIdContractHash = Settings.Default.FsIdContractHash;
            var script = containerContractHash.MakeScript("init", netMapContractHash.ToArray(), balanceContractHash.ToArray(), neofsIdContractHash.ToArray());
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash } },
                SystemFee = 1000000000,
                ValidUntilBlock = 1000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:");
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void FsIdContractHashContractInit()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var containerContractHash = Settings.Default.ContainerContractHash;
            var netMapContractHash = Settings.Default.NetmapContractHash;
            var neofsIdContractHash = Settings.Default.FsIdContractHash;
            var script = neofsIdContractHash.MakeScript("init", netMapContractHash.ToArray(), containerContractHash.ToArray());

            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 200000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash } },
                SystemFee = 1000000000,
                ValidUntilBlock = 1000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:");
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void AlphabetContractHashContractInit()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            for (int i = 0; i < Settings.Default.AlphabetContractHash.Length; i++) {
                var alphabetContractHash = Settings.Default.AlphabetContractHash[i];
                var netMapContractHash = Settings.Default.NetmapContractHash;
                var script = alphabetContractHash.MakeScript("init", netMapContractHash.ToArray());

                var tx = new Transaction()
                {
                    Attributes = Array.Empty<TransactionAttribute>(),
                    NetworkFee = 200000000,
                    Nonce = 1220,
                    Script = script,
                    Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[0].ScriptHash } },
                    SystemFee = 1000000000,
                    ValidUntilBlock = 1000,
                    Version = 0,
                };
                var data = new ContractParametersContext(tx);
                wallet1.Sign(data);
                tx.Witnesses = data.GetWitnesses();
                var snapshot = Blockchain.Singleton.GetSnapshot();
                //var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
                Console.WriteLine("tx:");
                Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
            }
        }

        [TestMethod()]
        public void NetMapEpoch()
        {
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var netMapContractHash = Settings.Default.NetmapContractHash;
            var script = netMapContractHash.MakeScript("newEpoch", 3);

            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accounts.ToArray()[2].ScriptHash ,Scopes=WitnessScope.Global} },
                SystemFee = 2000000000,
                ValidUntilBlock = 5000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            wallet1.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:"+engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void NetMapContractAddPeer()
        {
            var newwallet = new MyWallet("test");
            newwallet.Import("L2NpJUsXCm3ajA98bzFWFztjTNrXcfYU9xWzHZgUasvTSA6rnRrR");
            IEnumerable<WalletAccount> accountstemp = newwallet.GetAccounts();
            KeyPair key = accountstemp.ToArray()[0].GetKey();
            Console.WriteLine(key.PublicKeyHash.ToAddress());
            var nodeInfo = new NodeInfo()
            {
                PublicKey = Google.Protobuf.ByteString.CopyFrom(key.PublicKey.ToArray()),
                Address = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
                State = NodeInfo.Types.State.Online
            };

            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var netmapContractHash = Settings.Default.NetmapContractHash;
            var script = netmapContractHash.MakeScript("addPeer", nodeInfo.ToByteArray());
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 1220,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accountstemp.ToArray()[0].ScriptHash ,Scopes=WitnessScope.Global} },
                SystemFee = 10000000000,
                ValidUntilBlock = 8000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:"+engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void NetMapContractUpdateState()
        {
            var newwallet = new MyWallet("test");
            newwallet.Import("L2NpJUsXCm3ajA98bzFWFztjTNrXcfYU9xWzHZgUasvTSA6rnRrR");
            IEnumerable<WalletAccount> accountstemp = newwallet.GetAccounts();
            KeyPair key = accountstemp.ToArray()[0].GetKey();
            Console.WriteLine(key.PublicKeyHash.ToAddress());
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            var netmapContractHash = Settings.Default.NetmapContractHash;
            var script = netmapContractHash.MakeScript("updateState", (int)NodeInfo.Types.State.Offline, key.PublicKey.ToArray());
            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 1244,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accountstemp.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                SystemFee = 10000000000,
                ValidUntilBlock = 14000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:" + engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void ContainerContractPut()
        {
            var newwallet = new MyWallet("test");
            newwallet.Import("L2NpJUsXCm3ajA98bzFWFztjTNrXcfYU9xWzHZgUasvTSA6rnRrR");
            IEnumerable<WalletAccount> accountstemp = newwallet.GetAccounts();
            KeyPair key = accountstemp.ToArray()[0].GetKey();
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version()
                {
                    Major = 1,
                    Minor = 1,
                },
                BasicAcl = 0,
                Nonce = Google.Protobuf.ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new NeoFS.API.v2.Netmap.PlacementPolicy()
            };

            byte[] sig = Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            var containerContractHash = Settings.Default.ContainerContractHash;
            var script = containerContractHash.MakeScript("put", container.ToByteArray(), sig, key.PublicKey.EncodePoint(true));

            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 1244,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accountstemp.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                SystemFee = 2000000000,
                ValidUntilBlock = 5000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:" + engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }

        [TestMethod()]
        public void ContainerContractDelete()
        {
            var newwallet = new MyWallet("test");
            newwallet.Import("L2NpJUsXCm3ajA98bzFWFztjTNrXcfYU9xWzHZgUasvTSA6rnRrR");
            IEnumerable<WalletAccount> accountstemp = newwallet.GetAccounts();
            KeyPair key = accountstemp.ToArray()[0].GetKey();
            var wallet1 = TestBlockchain.wallet;
            var accounts = TestBlockchain.wallet.GetAccounts();
            OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version()
                {
                    Major = 1,
                    Minor = 1,
                },
                BasicAcl = 0,
                Nonce = Google.Protobuf.ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new NeoFS.API.v2.Netmap.PlacementPolicy()
            };

            byte[] sig = Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            var containerContractHash = Settings.Default.ContainerContractHash;
            var containerId = container.CalCulateAndGetID.Value.ToByteArray();
            sig = Crypto.Sign(containerId, key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            var script = containerContractHash.MakeScript("delete", containerId, sig);

            var tx = new Transaction()
            {
                Attributes = Array.Empty<TransactionAttribute>(),
                NetworkFee = 2000000000,
                Nonce = 1244,
                Script = script,
                Signers = new Signer[] { new Signer() { Account = accountstemp.ToArray()[0].ScriptHash, Scopes = WitnessScope.Global } },
                SystemFee = 10000000000,
                ValidUntilBlock = 5000,
                Version = 0,
            };
            var data = new ContractParametersContext(tx);
            newwallet.Sign(data);
            tx.Witnesses = data.GetWitnesses();
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var engine = ApplicationEngine.Run(script, snapshot, tx, null, 0, tx.SystemFee);
            Console.WriteLine("tx:" + engine.State);
            Console.WriteLine(Convert.ToBase64String(tx.ToArray()));
        }*/
    }
}

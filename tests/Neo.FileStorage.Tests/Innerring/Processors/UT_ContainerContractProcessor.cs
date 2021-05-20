using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using Neo.IO;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.Morph.Event.MorphEvent;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_ContainerContractProcessor : TestKit
    {
        private NeoSystem system;
        private ContainerContractProcessor processor;
        private Client morphclient;
        private Wallet wallet;
        private IActorRef actor;
        private TestUtils.TestState state;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            actor = this.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            morphclient = new Client()
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                    actor = actor
                }
            };
            state = new TestUtils.TestState() { alphabetIndex = 1 };
            processor = new ContainerContractProcessor()
            {
                MorphCli = morphclient,
                State = state,
                WorkPool = actor
            };
        }

        [TestMethod]
        public void HandlePutTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new API.Netmap.PlacementPolicy()
            };
            byte[] sig = Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);

            processor.HandlePut(new ContainerPutEvent()
            {
                RawContainer = container.ToByteArray(),
                PublicKey = key.PublicKey,
                Signature = sig
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleDeleteTest()
        {
            processor.HandleDelete(new ContainerDeleteEvent()
            {
                ContainerID = new byte[] { 0x01 },
                Signature = new byte[] { 0x01 }
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessContainerPutTest()
        {
            state.isAlphabet = true;
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new API.Netmap.PlacementPolicy()
            };
            byte[] sig = Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            processor.ProcessContainerPut(new ContainerPutEvent()
            {
                PublicKey = key.PublicKey,
                Signature = sig,
                RawContainer = container.ToByteArray()
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);

            state.isAlphabet = false;
            processor.ProcessContainerPut(new ContainerPutEvent()
            {
                PublicKey = key.PublicKey,
                Signature = sig,
                RawContainer = container.ToByteArray()
            });
            ExpectNoMsg();
        }

        [TestMethod]
        public void ProcessContainerDeleteTest()
        {
            state.isAlphabet = true;
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var containerId = "fc780e98b7970002a80fbbeb60f9ed6cf44d5696588ea32e4338ceaeda4adddc".HexToBytes();
            var sig = Cryptography.Crypto.Sign(containerId, key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            processor.ProcessContainerDelete(new ContainerDeleteEvent()
            {
                ContainerID = containerId,
                Signature = sig
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);

            state.isAlphabet = false;
            processor.ProcessContainerDelete(new ContainerDeleteEvent()
            {
                ContainerID = containerId,
                Signature = sig
            });
            ExpectNoMsg();
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 2);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 2);
        }

        [TestMethod]
        public void CheckFormatTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version()
                {
                    Major = 1,
                    Minor = 1,
                },
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new API.Netmap.PlacementPolicy()
            };
            Action action = () => processor.CheckFormat(container);
            //wrong nonce
            container.Nonce = ByteString.CopyFrom(new byte[15], 0, 15);
            Assert.ThrowsException<Exception>(action);
            //no placementpolicy
            container.PlacementPolicy = null;
            Assert.ThrowsException<Exception>(action);
        }
    }
}

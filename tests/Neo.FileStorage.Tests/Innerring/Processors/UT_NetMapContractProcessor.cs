using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.API.Netmap;
using Neo.IO;
using Neo.Wallets;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using static Neo.FileStorage.InnerRing.Timer.EpochTickEvent;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_NetMapContractProcessor : TestKit
    {
        private NeoSystem system;
        private NetMapContractProcessor processor;
        private MorphClient morphclient;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            wallet = TestBlockchain.wallet;
            morphclient = new MorphClient()
            {
                wallet = wallet,
                system = system
            };
            processor = new NetMapContractProcessor()
            {
                MorphCli = new Client() { client= morphclient },
                //ActiveState = new TestActiveState(),
                //EpochState = new EpochState(),
                //EpochTimerReseter = new EpochTimerReseter(),
                WorkPool = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor())),
                NetmapSnapshot = new NetMapContractProcessor.CleanupTable(true, 1)
            };
        }

        [TestMethod]
        public void HandleNewEpochTickTest()
        {
            processor.HandleNewEpochTick(new NewEpochTickEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleNewEpochTest()
        {
            processor.HandleNewEpoch(new NewEpochEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleAddPeerTest()
        {
            processor.HandleAddPeer(new AddPeerEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleUpdateStateTest()
        {
            IEnumerator<WalletAccount> accounts = wallet.GetAccounts().GetEnumerator();
            accounts.MoveNext();
            processor.HandleUpdateState(new UpdatePeerEvent()
            {
                PublicKey = accounts.Current.GetKey().PublicKey,
                Status = 2
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleCleanupTickTest()
        {
            processor.HandleCleanupTick(new NetmapCleanupTickEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 3);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 3);
        }

        [TestMethod]
        public void ListenerTimersHandlersTest()
        {
            var handlerInfos = processor.TimersHandlers();
            Assert.AreEqual(handlerInfos.Length, 1);
        }

        [TestMethod]
        public void ProcessNewEpochTest()
        {
            processor.ProcessNewEpoch(new NewEpochEvent()
            {
                EpochNumber = 1
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessNewEpochTickTest()
        {
            processor.ProcessNewEpochTick(new NewEpochTickEvent());
        }

        [TestMethod]
        public void ProcessAddPeerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new NodeInfo()
            {
                PublicKey = Google.Protobuf.ByteString.CopyFrom(key.PublicKey.ToArray()),
                Address = Neo.FileStorage.API.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
                State = NodeInfo.Types.State.Online
            };
            processor.ProcessAddPeer(new AddPeerEvent()
            {
                Node = nodeInfo.ToByteArray()
            });
        }

        [TestMethod]
        public void ProcessUpdateStateTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new NodeInfo()
            {
                PublicKey = Google.Protobuf.ByteString.CopyFrom(key.PublicKey.ToArray()),
                Address = Neo.FileStorage.API.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
                State = NodeInfo.Types.State.Online
            };
            processor.ProcessUpdateState(new UpdatePeerEvent()
            {
                PublicKey = key.PublicKey,
                Status = (int)NodeInfo.Types.State.Offline
            });
        }

        [TestMethod]
        public void ProcessNetmapCleanupTickTest()
        {
            processor.ProcessNetmapCleanupTick(new NetmapCleanupTickEvent()
            {
                Epoch = 1
            });
        }

        public class EpochTimerReseter //: IEpochTimerReseter
        {
            public void ResetEpochTimer()
            {
                return;
            }
        }
    }
}

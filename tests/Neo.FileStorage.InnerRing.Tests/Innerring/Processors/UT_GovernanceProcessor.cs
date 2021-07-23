using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Tests;
using Neo.IO;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using static Neo.FileStorage.InnerRing.Events.MorphEvent;
using static Neo.FileStorage.Morph.Event.MorphEvent;

namespace Neo.FileStorage.InnerRing.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_GovernanceProcessor : TestKit
    {
        private NeoSystem system;
        private GovernanceProcessor processor;
        private MorphInvoker morphInvoker;
        private MainInvoker mainInvoker;
        private Wallet wallet;
        private IActorRef actor;
        private TestState state;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            actor = this.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            mainInvoker = TestBlockchain.CreateTestMainInvoker(system, actor, wallet);
            morphInvoker = TestBlockchain.CreateTestMorphInvoker(system, actor, wallet);
            state = new TestState() { alphabetIndex = 1 };
            processor = new GovernanceProcessor()
            {
                MorphInvoker = morphInvoker,
                MainInvoker = mainInvoker,
                ProtocolSettings = system.Settings,
                State = state,
                WorkPool = actor
            };
        }

        [TestMethod]
        public void HandleAlphabetSyncTest()
        {
            processor.HandleAlphabetSync(new SyncEvent());
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessAlphabetSyncTest()
        {
            state.isAlphabet = true;
            state.morphInvoker = morphInvoker;
            processor.ProcessAlphabetSync();
            ExpectNoMsg();
        }

        [TestMethod]
        public void NewAlphabetListTest()
        {
            ECPoint[] sidechain = new ECPoint[] {
                ECPoint.DecodePoint(Convert.FromHexString("030551b149b5f3b34cb5f0bb90e3c60d2b269a99bb9b58a271fbe8f73fc9d54678"),ECCurve.Secp256r1),
                ECPoint.DecodePoint(Convert.FromHexString("036bbe8d0e8c0c257feec1f179c1036511ff64c686cf3d62b60ee56633f5d7fb13"),ECCurve.Secp256r1),
                ECPoint.DecodePoint(Convert.FromHexString("03261c49859f191eff7d1ac8fdd92cb8ea2d03083950042effc20df41f27243edd"),ECCurve.Secp256r1),
                ECPoint.DecodePoint(Convert.FromHexString("02b0704d818e3bcdcfceb9941edcf6daaee74dc6453fc22761590bfc4ac2ab8d7f"),ECCurve.Secp256r1)};
            ECPoint[] mainchain = new ECPoint[] {
                ECPoint.DecodePoint(Convert.FromHexString("030551b149b5f3b34cb5f0bb90e3c60d2b269a99bb9b58a271fbe8f73fc9d54678"),ECCurve.Secp256r1),
                ECPoint.DecodePoint(Convert.FromHexString("036bbe8d0e8c0c257feec1f179c1036511ff64c686cf3d62b60ee56633f5d7fb13"),ECCurve.Secp256r1),
                ECPoint.DecodePoint(Convert.FromHexString("0323e9c548dc7eda8e0f93f02be71e43cbc4f43f3905d6b9c6adc22df81a138a4c"),ECCurve.Secp256r1),
                ECPoint.DecodePoint(Convert.FromHexString("02b0704d818e3bcdcfceb9941edcf6daaee74dc6453fc22761590bfc4ac2ab8d7f"),ECCurve.Secp256r1)};
            processor.NewAlphabetList(sidechain, mainchain).ToList().ForEach(p => Console.WriteLine("NewAlphabetListTest:" + p.ToString()));
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 1);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 1);
        }
    }
}

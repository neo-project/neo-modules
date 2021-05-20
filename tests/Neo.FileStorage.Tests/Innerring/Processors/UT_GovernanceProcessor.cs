using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.Morph.Event.MorphEvent;
using Neo.Plugins.util;
using static Neo.FileStorage.InnerRing.Events.MorphEvent;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_GovernanceProcessor : TestKit
    {
        private NeoSystem system;
        private GovernanceProcessor processor;
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
            processor = new GovernanceProcessor()
            {
                MorphCli = morphclient,
                MainCli = morphclient,
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
            state.morphClient = morphclient;
            processor.ProcessAlphabetSync();
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 0);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 0);
        }
    }
}

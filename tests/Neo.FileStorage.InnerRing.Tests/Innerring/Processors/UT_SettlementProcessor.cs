using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing.Events;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Invoker.Morph;
using Neo.Wallets;

namespace Neo.FileStorage.InnerRing.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_SettlementProcessor : TestKit
    {
        private NeoSystem system;
        private SettlementProcessor processor;
        private MorphInvoker morphInvoker;
        private MainInvoker mainInvoker;
        private Wallet wallet;
        private TestState state;
        private IActorRef actor;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            actor = this.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            mainInvoker = TestBlockchain.CreateTestMainInvoker(system, actor, wallet);
            morphInvoker = TestBlockchain.CreateTestMorphInvoker(system, actor, wallet);
            state = new TestState() { alphabetIndex = 1 };
            var clientCache = new RpcClientCache() { wallet = wallet };
            var auditCalcDeps = new AuditSettlementDeps()
            {
                Invoker = morphInvoker,
                ClientCache = clientCache,
            };
            var basicSettlementDeps = new BasicIncomeSettlementDeps()
            {
                Invoker = morphInvoker,
            };
            var auditSettlementCalc = new Calculator(auditCalcDeps);
            processor = new SettlementProcessor()
            {
                BasicIncome = basicSettlementDeps,
                AuditProc = auditSettlementCalc,
                State = state,
                WorkPool = actor
            };
        }

        [TestMethod]
        public void HandleTest()
        {
            state.isActive = true;
            processor.Handle(0);
            ExpectNoMsg();
            processor.Handle(1);
            ExpectNoMsg();
        }

        [TestMethod]
        public void HandleAuditEventTest()
        {
            processor.HandleAuditEvent(new AuditStartEvent() { Epoch = 1 });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleIncomeCollectionEventTest()
        {
            state.isAlphabet = true;
            processor.HandleIncomeCollectionEvent(new BasicIncomeCollectEvent() { Epoch = 1 });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void HandleIncomeDistributionEventTest()
        {
            state.isAlphabet = true;
            processor.HandleIncomeDistributionEvent(new BasicIncomeDistributeEvent() { Epoch = 1 });
            ExpectNoMsg();
        }

        [TestMethod]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(0, handlerInfos.Length);
        }

        [TestMethod]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(0, parserInfos.Length);
        }
    }
}

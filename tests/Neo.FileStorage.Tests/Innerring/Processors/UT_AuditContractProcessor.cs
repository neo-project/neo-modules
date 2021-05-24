using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing;
using Neo.FileStorage.InnerRing.Processors;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Audit;
using Neo.Wallets;
using static Neo.FileStorage.Morph.Event.MorphEvent;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    [TestClass]
    public class UT_AuditContractProcessor : TestKit
    {
        private NeoSystem system;
        private AuditContractProcessor processor;
        private Client morphclient;
        private Wallet wallet;
        private TestUtils.TestState state;
        private IActorRef actor;

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
            state = new TestUtils.TestState() { alphabetIndex = 1, innerRingIndex = 0, innerRingSize = 1 };
            var auditTaskManager = system.ActorSystem.ActorOf(FakeAuditTaskManager.Props(TestActor));
            var clientCache = new RpcClientCache() { wallet = wallet };
            processor = new AuditContractProcessor()
            {
                MorphCli = morphclient,
                ClientCache = clientCache,
                TaskManager = auditTaskManager,
                State = state,
                WorkPool = actor
            };
        }

        [TestMethod]
        public void HandleNewAuditRoundTest()
        {
            processor.HandleNewAuditRound(new StartEvent() { epoch = 1 });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod]
        public void ProcessStartAuditTest()
        {
            processor.ProcessStartAudit(0);
            var nt = ExpectMsg<AuditTask>();
            Assert.IsNotNull(nt);
        }
    }

    public class FakeAuditTaskManager : UntypedActor
    {
        private IActorRef actor;

        public FakeAuditTaskManager(IActorRef actor)
        {
            this.actor = actor;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case AuditTask task:
                    actor.Tell(task);
                    break;
                case Manager.ResetMessage _:
                    Sender.Tell(0);
                    break;
            }
        }

        public static Props Props(IActorRef actor)
        {
            return Akka.Actor.Props.Create(() => new FakeAuditTaskManager(actor));
        }
    }
}

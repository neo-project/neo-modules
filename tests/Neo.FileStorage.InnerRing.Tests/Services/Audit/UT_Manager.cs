using System;
using System.Threading;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.InnerRing.Services.Audit;
using Neo.FileStorage.InnerRing.Services.Audit.Auditor;
using Neo.FileStorage.Utils;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Tests.Services.Audit
{
    [TestClass]
    public class UT_Manager : TestKit
    {
        public const int ManagerCapacity = 3;
        private IActorRef manager;

        public class Middleware : UntypedActor
        {
            private readonly IActorRef r;
            private readonly IActorRef wp;
            private IActorRef manager;

            public Middleware(IActorRef r, IActorRef wp)
            {
                this.wp = wp;
                this.r = r;
            }

            protected override void OnReceive(object message)
            {
                if (message is WorkerPool.NewTask t)
                {
                    bool result = wp.Ask<bool>(new WorkerPool.NewTask()
                    {
                        Process = t.Process,
                        Task = new(() =>
                        {
                            Thread.Sleep(2000);
                            r.Tell(new Manager.CompleteTask());
                            manager.Tell(new Manager.CompleteTask());
                        })
                    }).Result;
                    r.Tell(result);
                    Sender.Tell(result);
                }
                if (message is IActorRef m)
                    manager = m;
            }
        }

        public class Reporter : IReporter
        {
            public void WriteReport(Report r)
            {
                throw new NotImplementedException();
            }
        }

        public class TestContainerCommunacator : IContainerCommunicator
        {
            public StorageGroup GetStorageGroup(AuditTask task, ObjectID oid)
            {
                throw new NotImplementedException();
            }

            public FSObject GetHeader(AuditTask task, Node node, ObjectID oid, bool relay)
            {
                throw new NotImplementedException();
            }

            public byte[] GetRangeHash(AuditTask task, Node node, ObjectID oid, FSRange range)
            {
                throw new NotImplementedException();
            }
        }

        [TestInitialize]
        public void TestSetup()
        {
            const uint interval = 5000;
            IActorRef wp = Sys.ActorOf(WorkerPool.Props("Audit", ManagerCapacity));
            IActorRef mw = Sys.ActorOf(Props.Create(() => new Middleware(TestActor, wp)));
            manager = Sys.ActorOf(Manager.Props(ManagerCapacity, mw, () =>
            {
                return Sys.ActorOf(WorkerPool.Props("AuditPOR", ManagerCapacity));
            }, () =>
            {
                return Sys.ActorOf(WorkerPool.Props("AuditPDP", ManagerCapacity));
            }, new TestContainerCommunacator(), interval));
            mw.Tell(manager);
        }

        [TestMethod]
        public void TestOneTask()
        {
            manager.Tell(new AuditTask());
            Assert.IsTrue(ExpectMsg<bool>());
        }

        [TestMethod]
        public void TestRedundantTaskAndReset()
        {
            int redundant = 3;
            for (int i = 0; i < ManagerCapacity; i++)
            {
                manager.Tell(new AuditTask());
                Assert.IsTrue(ExpectMsg<bool>());
            }
            for (int i = 0; i < redundant; i++)
            {
                manager.Tell(new AuditTask());
                Assert.IsFalse(ExpectMsg<bool>());
            }
            Assert.AreEqual(redundant, manager.Ask<int>(new Manager.ResetMessage()).Result);
        }

        [TestMethod]
        public void TestConsume()
        {
            int redundant = 3;
            for (int i = 0; i < ManagerCapacity; i++)
            {
                manager.Tell(new AuditTask());
                Assert.IsTrue(ExpectMsg<bool>());
            }
            for (int i = 0; i < redundant; i++)
            {
                manager.Tell(new AuditTask());
                Assert.IsFalse(ExpectMsg<bool>());
            }
            Thread.Sleep(4000);
            Assert.AreEqual(0, manager.Ask<int>(new Manager.ResetMessage()).Result);
        }
    }
}

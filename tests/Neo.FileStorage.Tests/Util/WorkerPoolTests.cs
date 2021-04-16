using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System;
using Neo.FileStorage.Utils;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.Tests.Util
{
    [TestClass]
    public class WorkerPoolTests : TestKit
    {
        private NeoSystem system;
        private IActorRef workerpool;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            workerpool = system.ActorSystem.ActorOf(WorkerPool.Props("test", 2));
        }

        [TestMethod]
        public void NewTaskAndCompleteTaskTest()
        {
            workerpool.Tell(new NewTask() { process = "aaa", task = new Task(() => { Console.WriteLine("aaa"); }) });
        }
    }
}

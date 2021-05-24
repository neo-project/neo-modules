using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.InnerRing.Timer;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Tests.InnerRing.Processors;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Neo.FileStorage.InnerRing.Timer.BlockTimer;

namespace Neo.FileStorage.Tests.Innerring.Timer
{
    [TestClass]
    public class UT_BlockTimer:TestKit
    {
        private NeoSystem system;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
        }

        [TestMethod]
        public void OnTickTest() {
            uint dur = 2;
            var blockTimer = system.ActorSystem.ActorOf(BlockTimer.Props(() => { return dur; }, () => { this.TestActor.Tell(new Object()); }));
            blockTimer.Tell(new ResetEvent());
            blockTimer.Tell(new TickEvent());
            ExpectNoMsg();
            blockTimer.Tell(new TickEvent());
            ExpectMsg<Object>();
        }
    }
}

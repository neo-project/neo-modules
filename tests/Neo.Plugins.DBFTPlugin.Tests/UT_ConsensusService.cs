using System;
using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Wallets;

namespace Neo.Consensus;

[TestClass]
public class UT_ConsensusService : TestKit
{
    private static readonly Random TestRandom = new Random(1337); // use fixed seed for guaranteed determinism

    ConsensusService uut;

    [TestCleanup]
    public void Cleanup()
    {
        Shutdown();
    }

    [TestInitialize]
    public void TestSetup()
    {
        var mockNeoSystem = new Mock<TestNeoSystem>();
        var mockSetting = new Mock<TestSetting>();
        var mockWallet = new Mock<Wallet>();
        uut = new ConsensusService(mockNeoSystem.Object, mockSetting.Object, mockWallet.Object);
    }

    // [TestMethod]
    // public void ConsensusService_Test_IsHighPriority()
    // {
    //     // high priority
    //     uut.IsHighPriority(new ExtensiblePayload()).Should().Be(true);
    //     uut.IsHighPriority(new ConsensusService.SetViewNumber()).Should().Be(true);
    //     uut.IsHighPriority(new ConsensusService.Timer()).Should().Be(true);
    //     uut.IsHighPriority(new Blockchain.PersistCompleted()).Should().Be(true);
    //
    //     // any random object should not have priority
    //     object obj = null;
    //     uut.IsHighPriority(obj).Should().Be(false);
    // }
}

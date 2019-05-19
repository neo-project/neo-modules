using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using Neo.Persistence;
using Settings = Neo.Plugins.Settings;
using System.Collections.Generic;
using System.Linq;
using System;
using Moq;

namespace SimplePolicy.UnitTests
{
    [TestClass]
    public class UT_SimplePolicy
    {
        private static Random _random = new Random(11121990);

        SimplePolicyPlugin uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new SimplePolicyPlugin();
        }

        [TestMethod]
        public void TestMaxTransactionsPerBlock()
        {
            Settings.Default.MaxTransactionsPerBlock.Should().Be(500);
            Settings.Default.MaxFreeTransactionsPerBlock.Should().Be(20);
        }

        [TestMethod]
        public void FreeTxVerifySort_NoHighPriority()
        {
            List<Transaction> txList = new List<Transaction>();
            // three different sizes, but it doesn't matter
            for (var size = 100; size <= 200; size += 50)
            {
                for (var netFeeSatoshi = 0; netFeeSatoshi <= 90000; netFeeSatoshi += 10000)
                {
                    var testTx = MockGenerateTransaction(netFeeSatoshi, size).Object;
                    testTx.IsLowPriority.Should().Be(true); // "LowPriorityThreshold": 0.001
                    txList.Insert(0, testTx);
                }
            }

            txList.Count.Should().Be(30);
            // transactions => size: [10, 15, 20] x price: [0 ... 90000, step by 10000]
            //foreach(var tx in txList)
            //    Console.WriteLine($"TX fee: {tx.NetworkFee} size: {tx.Size} ratio: {tx.FeePerByte}");
            /*
             TX fee: 0.0009 size: 20 ratio: 0.000045
             TX fee: 0.0008 size: 20 ratio: 0.00004
             TX fee: 0.0007 size: 20 ratio: 0.000035
             TX fee: 0.0006 size: 20 ratio: 0.00003
             TX fee: 0.0005 size: 20 ratio: 0.000025
             TX fee: 0.0004 size: 20 ratio: 0.00002
             TX fee: 0.0003 size: 20 ratio: 0.000015
             TX fee: 0.0002 size: 20 ratio: 0.00001
             TX fee: 0.0001 size: 20 ratio: 0.000005
             TX fee: 0 size: 20 ratio: 0
             TX fee: 0.0009 size: 15 ratio: 0.00006
             TX fee: 0.0008 size: 15 ratio: 0.00005333
             TX fee: 0.0007 size: 15 ratio: 0.00004666
             TX fee: 0.0006 size: 15 ratio: 0.00004
             TX fee: 0.0005 size: 15 ratio: 0.00003333
             TX fee: 0.0004 size: 15 ratio: 0.00002666
             TX fee: 0.0003 size: 15 ratio: 0.00002
             TX fee: 0.0002 size: 15 ratio: 0.00001333
             TX fee: 0.0001 size: 15 ratio: 0.00000666
             TX fee: 0 size: 15 ratio: 0
             TX fee: 0.0009 size: 10 ratio: 0.00009
             TX fee: 0.0008 size: 10 ratio: 0.00008
             TX fee: 0.0007 size: 10 ratio: 0.00007
             TX fee: 0.0006 size: 10 ratio: 0.00006
             TX fee: 0.0005 size: 10 ratio: 0.00005
             TX fee: 0.0004 size: 10 ratio: 0.00004
             TX fee: 0.0003 size: 10 ratio: 0.00003
             TX fee: 0.0002 size: 10 ratio: 0.00002
             TX fee: 0.0001 size: 10 ratio: 0.00001
            */

            IEnumerable<Transaction> filteredTxList = uut.FilterForBlock(txList);
            filteredTxList.Count().Should().Be(20);

            // will select top 20
            // part A: 18 transactions with ratio >= 0.000025
            // part B: 2 transactions with ratio = 0.00002 (but one with this ratio will be discarded, with fee 0.0002)
            //foreach(var tx in filteredTxList)
            //    Console.WriteLine($"TX20 fee: {tx.NetworkFee} size: {tx.Size} ratio: {tx.NetworkFee / tx.Size}");
            /*
            TX20 fee: 0.0009 size: 10 ratio: 0.00009
            TX20 fee: 0.0008 size: 10 ratio: 0.00008
            TX20 fee: 0.0007 size: 10 ratio: 0.00007
            TX20 fee: 0.0009 size: 15 ratio: 0.00006
            TX20 fee: 0.0006 size: 10 ratio: 0.00006
            TX20 fee: 0.0008 size: 15 ratio: 0.00005333
            TX20 fee: 0.0005 size: 10 ratio: 0.00005
            TX20 fee: 0.0007 size: 15 ratio: 0.00004666
            TX20 fee: 0.0009 size: 20 ratio: 0.000045
            TX20 fee: 0.0008 size: 20 ratio: 0.00004
            TX20 fee: 0.0006 size: 15 ratio: 0.00004
            TX20 fee: 0.0004 size: 10 ratio: 0.00004
            TX20 fee: 0.0007 size: 20 ratio: 0.000035
            TX20 fee: 0.0005 size: 15 ratio: 0.00003333
            TX20 fee: 0.0006 size: 20 ratio: 0.00003
            TX20 fee: 0.0003 size: 10 ratio: 0.00003
            TX20 fee: 0.0004 size: 15 ratio: 0.00002666
            TX20 fee: 0.0005 size: 20 ratio: 0.000025
            TX20 fee: 0.0004 size: 20 ratio: 0.00002
            TX20 fee: 0.0003 size: 15 ratio: 0.00002
            */

            // part A
            filteredTxList.Where(tx => (tx.NetworkFee / tx.Size) >= 250).Count().Should().Be(18); // 18 enter in part A
            txList.Where(tx => (tx.NetworkFee / tx.Size) >= 250).Count().Should().Be(18); // they also exist in main list
            txList.Where(tx => (tx.NetworkFee / tx.Size) < 250).Count().Should().Be(30 - 18); // 12 not selected transactions in part A
            // part B
            filteredTxList.Where(tx => (tx.NetworkFee / tx.Size) < 250).Count().Should().Be(2); // only two enter in part B
            filteredTxList.Where(tx => (tx.NetworkFee / tx.Size) == 200).Count().Should().Be(2); // only two enter in part B with ratio 0.00002
            txList.Where(tx => (tx.NetworkFee / tx.Size) == 200).Count().Should().Be(3); // 3 in tie (ratio 0.00002)
            txList.Where(tx => (tx.NetworkFee / tx.Size) == 200 && (tx.NetworkFee > 20000)).Count().Should().Be(2); // only 2 survive (fee > 0.0002)
        }

        [TestMethod]
        public void TestMock_GenerateTransaction()
        {
            var txHighPriority = MockGenerateTransaction(100000000, 50);
            // testing default values
            long txHighPriority_ratio = txHighPriority.Object.NetworkFee / txHighPriority.Object.Size;
            txHighPriority_ratio.Should().Be(2000000); // 0.02
            txHighPriority.Object.IsLowPriority.Should().Be(false);

            var txLowPriority = MockGenerateTransaction(100000000 / 10000, 50); // 0.00001
            // testing default values
            long txLowPriority_ratio = txLowPriority.Object.NetworkFee / txLowPriority.Object.Size;
            txLowPriority_ratio.Should().Be(200); // 0.000002  -> 200 satoshi / Byte
            txLowPriority.Object.IsLowPriority.Should().Be(true);
        }

        // Generate Mock InvocationTransaction with different sizes and prices
        public static Mock<Transaction> MockGenerateTransaction(long networkFee, int size)
        {
            var mockTx = new Mock<Transaction>
            {
                CallBase = true
            };

            mockTx.Setup(p => p.Verify(It.IsAny<Snapshot>(), It.IsAny<IEnumerable<Transaction>>())).Returns(true);
            var tx = mockTx.Object;
            tx.Script = new byte[0];
            tx.Sender = UInt160.Zero;
            tx.NetworkFee = networkFee;
            tx.Attributes = new TransactionAttribute[0];
            tx.Witnesses = new Witness[0];

            int diff = size - tx.Size;
            if (diff < 0) throw new InvalidOperationException();
            if (diff > 0)
            {
                tx.Script = new byte[diff];
                _random.NextBytes(tx.Script);
            }

            return mockTx;
        }
    }
}

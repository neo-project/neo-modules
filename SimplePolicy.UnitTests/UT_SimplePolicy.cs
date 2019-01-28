using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using Neo.Persistence;
using Settings = Neo.Plugins.Settings;
using System.Collections.Generic;
using Neo.Cryptography;
using System.Numerics;
using System.Collections;
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
        public void TestFilterForBlock_ClaimHasPriority()
        {
            // Should contain "ClaimTransaction" in "HighPriorityTxType"
            Settings.Default.HighPriorityTxType.Contains(TransactionType.ClaimTransaction).Should().Be(true);

            ClaimTransaction claimTxZero1 = GetClaimTransaction(0);
            claimTxZero1.Size.Should().Be(7 + 21); // 7 + 21 (random script)
            claimTxZero1.Hash.ToString().Should().Be("0x60037520be0fd903703c2b67973296f22cac8932db07a2723addf79478aea75f");
            ClaimTransaction claimTxZero2 = GetClaimTransaction(0);
            claimTxZero2.Size.Should().Be(7 + 21); // 7 + 21 (random script)
            claimTxZero2.Hash.ToString().Should().Be("0xb29426673b3ef5c226bd35d53c2cb2242e09c06f0efe9c0d5be2034f41cb85ba");
            ClaimTransaction claimTxZero3 = GetClaimTransaction(0);
            claimTxZero3.Size.Should().Be(7 + 21); // 7 + 21 (random script)
            claimTxZero3.Hash.ToString().Should().Be("0x01027faead9a0538048db7ac5657172f6e2240bff3f7d902e490bb1bd75c2df7");

            //ClaimTransaction claimTxTwo = GetClaimTransaction(2);
            //claimTxTwo.Size.Should().Be(75 + 21); // 2*34 + 7 + 21

            ClaimTransaction claimTx30 = GetClaimTransaction(30);
            claimTx30.Size.Should().Be(1027 + 21); // 30*34 + 7 + 21
            claimTx30.NetworkFee.Should().Be(Fixed8.Zero);
            claimTx30.IsLowPriority.Should().Be(true); // by default is Low Priority, but plugin makes it High Priority
            //uut.IsLowPriority -> cannot inspect because it's private... no problem!

            List<Transaction> TxList = new List<Transaction>();
            TxList.Insert(0, claimTxZero1);
            TxList.Insert(0, claimTxZero2);
            TxList.Insert(0, claimTxZero3);
            TxList.Insert(0, claimTx30);

            //Console.WriteLine("Tx List Claim");
            //foreach(var tx in TxList)
            //    Console.WriteLine($"Claim TX fee: {tx.NetworkFee} size: {tx.Size} ratio: {tx.FeePerByte} hash: {tx.Hash}" );


            // ======================== BEGIN TESTS ============================

            // insert 100 paid invocation transactions
            for (var i = 0; i < 100; i++)
            {
                TxList.Insert(0, MockGenerateInvocationTransaction(Fixed8.One, 50).Object);
            }

            // insert 100 low priority invocation transactions (18 satoshi + 82 zero)
            for (var i = 0; i < 100; i++)
            {
                if (i < 18)
                    TxList.Insert(0, MockGenerateInvocationTransaction(Fixed8.Satoshi, 50).Object);
                else
                    TxList.Insert(0, MockGenerateInvocationTransaction(Fixed8.Zero, 50).Object);
            }

            TxList.Count().Should().Be(204); // 100 free + 100 paid + 4 claims
            TxList.Where(tx => tx.NetworkFee == Fixed8.Zero).Count().Should().Be(100-18+4); // 82 fully free + 4 claims

            IEnumerable<Transaction> filteredTxList = uut.FilterForBlock(TxList);
            //filteredTxList.Count().Should().Be(124); // 20 free + 100 paid + 4 claims
            filteredTxList.Count().Should().Be(120); // 20 free (including 2 claims) + 100 paid
            filteredTxList.Where(tx => tx.NetworkFee == Fixed8.Zero).Count().Should().Be(2); // 2 fully free (2 claims)

            // will select 20 low priority (including Claims)
            var vx = filteredTxList.Where(tx => tx.IsLowPriority == true);
            vx.Count().Should().Be(20);

            // two Claim Transaction will survive
            vx = filteredTxList.Where(tx => tx.Type == TransactionType.ClaimTransaction);
            vx.Count().Should().Be(2);

            // =================================================================

            // insert more paid invocation transactions (400 of each)
            for (var i = 0; i < 400; i++)
            {
                TxList.Insert(0, MockGenerateInvocationTransaction(Fixed8.One, 50).Object);
            }

            // insert more free invocation transactions (400 of each)
            for (var i = 0; i < 400; i++)
            {
                TxList.Insert(0, MockGenerateInvocationTransaction(Fixed8.Zero, 50).Object);
            }

            TxList.Count().Should().Be(1004); // 500 free + 500 paid + 4 claims
            TxList.Where(tx => tx.NetworkFee == Fixed8.Zero).Count().Should().Be(400+100-18+4); // 500-18 fully free + 4 claims

            filteredTxList = uut.FilterForBlock(TxList);
            filteredTxList.Count().Should().Be(499); // full block

            // will select 20 low priority (including Claims)
            vx = filteredTxList.Where(tx => tx.IsLowPriority == true);
            vx.Count().Should().Be(20);

            // will still select Claim Transactions
            vx = filteredTxList.Where(tx => tx.Type == TransactionType.ClaimTransaction);
            vx.Count().Should().Be(2);

            // there are 3 tied Claim tx, will solve it based on smaller hash (0x01, 0x60) => 0xb2 is excluded
            // 0x01027faead9a0538048db7ac5657172f6e2240bff3f7d902e490bb1bd75c2df7
            // 0x60037520be0fd903703c2b67973296f22cac8932db07a2723addf79478aea75f
            // 0xb29426673b3ef5c226bd35d53c2cb2242e09c06f0efe9c0d5be2034f41cb85ba
            vx = filteredTxList.Where(tx => tx.Hash.ToString() == "0x01027faead9a0538048db7ac5657172f6e2240bff3f7d902e490bb1bd75c2df7");
            vx.Count().Should().Be(1);
            vx = filteredTxList.Where(tx => tx.Hash.ToString() == "0x60037520be0fd903703c2b67973296f22cac8932db07a2723addf79478aea75f");
            vx.Count().Should().Be(1);
            vx = filteredTxList.Where(tx => tx.Hash.ToString() == "0xb29426673b3ef5c226bd35d53c2cb2242e09c06f0efe9c0d5be2034f41cb85ba");
            vx.Count().Should().Be(0);

            //Console.WriteLine("filtered");
            //foreach(var tx in filteredTxList)
            //    Console.WriteLine($"TX fee: {tx.NetworkFee} size: {tx.Size} ratio: {tx.FeePerByte} hash: {tx.Hash}" );

        }


        [TestMethod]
        public void FreeTxVerifySort_NoHighPriority()
        {
            List<Transaction> txList = new List<Transaction>();
            // three different sizes, but it doesn't matter
            for (var size = 10; size <= 20; size += 5)
            {
                for (var netFeeSatoshi = 0; netFeeSatoshi <= 90000; netFeeSatoshi += 10000)
                {
                    var testTx = MockGenerateInvocationTransaction(new Fixed8(netFeeSatoshi), size).Object;
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
            filteredTxList.Where(tx => (tx.NetworkFee / tx.Size) >= new Fixed8(2500)).Count().Should().Be(18); // 18 enter in part A
            txList.Where(tx => (tx.NetworkFee / tx.Size) >= new Fixed8(2500)).Count().Should().Be(18); // they also exist in main list
            txList.Where(tx => (tx.NetworkFee / tx.Size) < new Fixed8(2500)).Count().Should().Be(30 - 18); // 12 not selected transactions in part A
            // part B
            filteredTxList.Where(tx => (tx.NetworkFee / tx.Size) < new Fixed8(2500)).Count().Should().Be(2); // only two enter in part B
            filteredTxList.Where(tx => (tx.NetworkFee / tx.Size) == new Fixed8(2000)).Count().Should().Be(2); // only two enter in part B with ratio 0.00002
            txList.Where(tx => (tx.NetworkFee / tx.Size) == new Fixed8(2000)).Count().Should().Be(3); // 3 in tie (ratio 0.00002)
            txList.Where(tx => (tx.NetworkFee / tx.Size) == new Fixed8(2000) && (tx.NetworkFee > new Fixed8(20000))).Count().Should().Be(2); // only 2 survive (fee > 0.0002)
        }


        [TestMethod]
        public void FreeTxVerifySortWithPriority()
        {
            List<Transaction> txList = new List<Transaction>();
            // three different sizes, but it doesn't matter
            for (var size = 10; size <= 15; size += 5)
            {
                for (var netFeeSatoshi = 0; netFeeSatoshi <= 90000; netFeeSatoshi += 10000)
                {
                    var testTx = MockGenerateInvocationTransaction(new Fixed8(netFeeSatoshi), size).Object;
                    testTx.IsLowPriority.Should().Be(true); // "LowPriorityThreshold": 0.001
                    txList.Insert(0, testTx);
                }
            }

            txList.Insert(0, GetClaimTransaction(1));
            txList.Insert(0, GetClaimTransaction(10));
            txList.Insert(0, GetClaimTransaction(20));
            txList.Insert(0, GetClaimTransaction(30));

            txList.Count.Should().Be(24); // 20 free + 4 claims

            IEnumerable<Transaction> filteredTxList = uut.FilterForBlock(txList);
            filteredTxList.Count().Should().Be(20);

            filteredTxList.Where(tx => tx.Type == TransactionType.ClaimTransaction).Count().Should().Be(2); // 2 claims will be selected
        }


        [TestMethod]
        public void TestMock_GenerateInvocationTransaction()
        {
            var txHighPriority = MockGenerateInvocationTransaction(Fixed8.One, 50);
            // testing default values
            Fixed8 txHighPriority_ratio = txHighPriority.Object.NetworkFee / txHighPriority.Object.Size;
            txHighPriority_ratio.Should().Be(new Fixed8(2000000)); // 0.02
            txHighPriority.Object.IsLowPriority.Should().Be(false);

            var txLowPriority = MockGenerateInvocationTransaction(Fixed8.One / 10000, 50); // 0.00001
            // testing default values
            Fixed8 txLowPriority_ratio = txLowPriority.Object.NetworkFee / txLowPriority.Object.Size;
            txLowPriority_ratio.Should().Be(new Fixed8(200)); // 0.000002  -> 200 satoshi / Byte
            txLowPriority.Object.IsLowPriority.Should().Be(true);
        }

        // Generate Mock InvocationTransaction with different sizes and prices
        public static Mock<InvocationTransaction> MockGenerateInvocationTransaction(Fixed8 networkFee, int size)
        {
            var mockTx = new Mock<InvocationTransaction>();
            mockTx.SetupGet(mr => mr.NetworkFee).Returns(networkFee);
            mockTx.SetupGet(mr => mr.Size).Returns(size);

            //==============================
            //=== Generating random Hash ===
            mockTx.CallBase = true;
            mockTx.Setup(p => p.Verify(It.IsAny<Snapshot>(), It.IsAny<IEnumerable<Transaction>>())).Returns(true);
            var tx = mockTx.Object;
            var randomBytes = new byte[16];
            _random.NextBytes(randomBytes);
            tx.Script = randomBytes;
            tx.Attributes = new TransactionAttribute[0];
            tx.Inputs = new CoinReference[0];
            tx.Outputs = new TransactionOutput[0];
            tx.Witnesses = new Witness[0];
            //==============================

            return mockTx;
        }


        // Create ClaimTransaction with 'countRefs' CoinReferences
        public static ClaimTransaction GetClaimTransaction(int countRefs)
        {
            CoinReference[] refs = new CoinReference[countRefs];
            for (var i = 0; i < countRefs; i++)
            {
                refs[i] = GetCoinReference(new UInt256(Crypto.Default.Hash256(new BigInteger(i).ToByteArray())));
            }

            //==============================
            //=== Generating random Hash ===
            var randomBytes = new byte[20];
            _random.NextBytes(randomBytes);
            //==============================
            return new ClaimTransaction
            {
                Claims = refs,
                Attributes = new TransactionAttribute[]{new TransactionAttribute{Usage = TransactionAttributeUsage.Script, Data = randomBytes} },
                Inputs = new CoinReference[0],
                Outputs = new TransactionOutput[0],
                Witnesses = new Witness[0]
            };
        }

        public static CoinReference GetCoinReference(UInt256 prevHash)
        {
            if (prevHash == null) prevHash = UInt256.Zero;
            return new CoinReference
            {
                PrevHash = prevHash,
                PrevIndex = 0
            };
        }
    }
}

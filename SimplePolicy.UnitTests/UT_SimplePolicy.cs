using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using System.Collections.Generic;
using Neo.Cryptography;
using System.Numerics;
using System.Collections;
using System.Linq;
using Moq;

namespace SimplePolicy.UnitTests
{
    [TestClass]
    public class UT_SimplePolicy
    {
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

            ClaimTransaction claimTxZero = GetClaimTransaction(0);
            claimTxZero.Size.Should().Be(7); // 7
            ClaimTransaction claimTxOne = GetClaimTransaction(1);
            claimTxOne.Size.Should().Be(41); // 34 + 7
            ClaimTransaction claimTxTwo = GetClaimTransaction(2);
            claimTxTwo.Size.Should().Be(75); // 2*34 + 7

            ClaimTransaction claimTx30 = GetClaimTransaction(30);
            claimTx30.Size.Should().Be(1027); // 30*34 + 7
            claimTx30.IsLowPriority.Should().Be(true); // by default is Low Priority, but plugin makes it High Priority
            //uut.IsLowPriority -> cannot inspect because it's private... no problem!


            List<Transaction> TxList = new List<Transaction>();
            TxList.Insert(0, claimTxZero);
            TxList.Insert(0, claimTxOne);
            TxList.Insert(0, claimTxTwo);
            TxList.Insert(0, claimTx30);


            var txHighPriority = new Mock<Transaction>(TransactionType.InvocationTransaction);
            txHighPriority.SetupGet(mr => mr.NetworkFee).Returns(Fixed8.One);
            txHighPriority.SetupGet(mr => mr.Size).Returns(50);
            Fixed8 txHighPriority_ratio = txHighPriority.Object.NetworkFee / txHighPriority.Object.Size;
            txHighPriority_ratio.Should().Be(new Fixed8(2000000)); // 0.02
            txHighPriority.Object.IsLowPriority.Should().Be(false);

            var txLowPriority = new Mock<Transaction>(TransactionType.InvocationTransaction);
            txLowPriority.SetupGet(mr => mr.NetworkFee).Returns(Fixed8.One/10000); // 0.00001
            txLowPriority.SetupGet(mr => mr.Size).Returns(50);
            Fixed8 txLowPriority_ratio = txLowPriority.Object.NetworkFee / txLowPriority.Object.Size;
            txLowPriority_ratio.Should().Be(new Fixed8(200)); // 0.000002  -> 200 satoshi / Byte
            txLowPriority.Object.IsLowPriority.Should().Be(true);

            // ======================== BEGIN TESTS ============================

            // insert invocation transactions (100 of each)
            for(var i=0; i<100; i++)
            {
                TxList.Insert(0, txHighPriority.Object);
                TxList.Insert(0, txLowPriority.Object);
            }

            TxList.Count().Should().Be(204); // 100 free + 100 paid + 4 claims

            IEnumerable<Transaction> filteredTxList = uut.FilterForBlock(TxList);
            filteredTxList.Count().Should().Be(124); // 20 free + 100 paid + 4 claims

            // all Claim Transaction will survive
            var vx = filteredTxList.Where(tx => tx.Type == TransactionType.ClaimTransaction);
            vx.Count().Should().Be(4);

            // =================================================================

            // insert more invocation transactions (400 of each)
            for(var i=0; i<400; i++)
            {
                TxList.Insert(0, txHighPriority.Object);
                TxList.Insert(0, txLowPriority.Object);
            }

            TxList.Count().Should().Be(1004); // 500 free + 500 paid + 4 claims

            filteredTxList = uut.FilterForBlock(TxList);
            filteredTxList.Count().Should().Be(499); // full block

            // will not select any Claim Transaction
            vx = filteredTxList.Where(tx => tx.Type == TransactionType.ClaimTransaction);
            vx.Count().Should().Be(0);

            // will select 20 low priority
            vx = filteredTxList.Where(tx => tx.IsLowPriority == true);
            vx.Count().Should().Be(20);
        }

        // Create ClaimTransaction with 'countRefs' CoinReferences
        public static ClaimTransaction GetClaimTransaction(int countRefs)
        {
            CoinReference[] refs = new CoinReference[countRefs];
            for(var i = 0; i<countRefs; i++)
            {
                refs[i] = GetCoinReference(new UInt256(Crypto.Default.Hash256(new BigInteger(i).ToByteArray())));
            }

            return new ClaimTransaction
            {
                Claims = refs,
                Attributes = new TransactionAttribute[0],
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

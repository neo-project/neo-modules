using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.Plugins;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native.Tokens;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_RpcServer
    {
        [TestMethod]
        public void Test_ConvertIEnumeratorToArray()
        {
            var a = new byte[100][];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = "NeHNBbeLNtiCEeaFQ6tLLpXkr5Xw6esKnV".ToScriptHash().ToArray();
            }
            IReadOnlyList<byte[]> values = a;
            
            IIterator iterator = new TestIterator(values);

            StackItem[] stackItems = new StackItem[]
            {
                12345,
                "hello",
                new InteropInterface(Enumerable.Range(1, 100).GetEnumerator()),
                new InteropInterface(new AccountState[] {new AccountState()}.GetEnumerator()),
                new InteropInterface(iterator)
            };

            Assert.AreEqual(StackItemType.InteropInterface, stackItems[2].Type);
            Assert.AreEqual(StackItemType.InteropInterface, stackItems[3].Type);
            Assert.AreEqual(StackItemType.InteropInterface, stackItems[4].Type);

            RpcServer.ConvertIEnumeratorToArray(stackItems, 1);

            Assert.AreEqual(StackItemType.Array, stackItems[2].Type);
            Assert.AreEqual(50, ((VM.Types.Array)stackItems[2]).Count);
            Assert.AreEqual(new InteropInterface(51), ((VM.Types.Array)stackItems[2])[0]);

            Assert.AreEqual(StackItemType.Array, stackItems[3].Type);

            Assert.AreEqual(StackItemType.Array, stackItems[4].Type);
            Assert.AreEqual(50, ((VM.Types.Array)stackItems[4]).Count);
            Assert.AreEqual(new ByteString("NeHNBbeLNtiCEeaFQ6tLLpXkr5Xw6esKnV".ToScriptHash().ToArray()), ((VM.Types.Array)stackItems[4])[0]);
        }
    }

    internal class TestIterator : IIterator
    {
        private readonly IReadOnlyList<byte[]> values;
        private int index = -1;

        public TestIterator(IReadOnlyList<byte[]> vs)
        {
            this.values = vs;
        }

        public PrimitiveType Key()
        {
            if (index < 0)
                throw new InvalidOperationException();
            return index;
        }

        public bool Next()
        {
            int next = index + 1;
            if (next >= values.Count)
                return false;
            index = next;
            return true;
        }

        public StackItem Value()
        {
            if (index < 0)
                throw new InvalidOperationException();
            return values[index];
        }

        public void Dispose()
        {
        }
    }
}

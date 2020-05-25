using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.Plugins;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native.Tokens;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_RpcServer
    {
        [TestMethod]
        public void Test_ConvertIEnumeratorToArray()
        {
            IReadOnlyList<byte[]> values = new byte[1][] { "NeHNBbeLNtiCEeaFQ6tLLpXkr5Xw6esKnV".ToScriptHash().ToArray() };
            IIterator iterator = new TestIterator(values);

            StackItem[] stackItems = new StackItem[]
            {
                12345,
                "hello",
                new InteropInterface(new int[] { 1, 2, 3 }.GetEnumerator()),
                new InteropInterface(new Nep5AccountState[] {new Nep5AccountState()}.GetEnumerator()),
                new InteropInterface(iterator)
            };

            Assert.AreEqual(stackItems[3].Type, StackItemType.InteropInterface);
            Assert.AreEqual(stackItems[4].Type, StackItemType.InteropInterface);

            var newStackItems = RpcServer.ConvertIEnumeratorToArray(stackItems);

            Assert.AreEqual(stackItems[3].Type, StackItemType.Array);
            Assert.AreEqual(stackItems[4].Type, StackItemType.Array);
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

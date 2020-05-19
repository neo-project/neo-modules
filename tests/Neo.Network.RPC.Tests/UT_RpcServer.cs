using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.SmartContract.Native.Tokens;
using Neo.VM.Types;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_RpcServer
    {
        [TestMethod]
        public void Test_ConvertIEnumeratorToArray()
        {
            StackItem[] stackItems = new StackItem[]
            {
                12345,
                "hello",
                new InteropInterface(new int[] { 1, 2, 3 }.GetEnumerator()),
                new InteropInterface(new Nep5AccountState[] {new Nep5AccountState()}.GetEnumerator())
            };

            Assert.AreEqual(stackItems[3].Type, StackItemType.InteropInterface);

            var newStackItems = RpcServer.ConvertIEnumeratorToArray(stackItems);

            Assert.AreEqual(stackItems[3].Type, StackItemType.Array);
        }
    }
}

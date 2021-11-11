using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Neo.Cryptography.MPTTrie.Tests
{
    [TestClass]
    public class UT_Helper
    {
        [TestMethod]
        public void TestCompareTo()
        {
            var arr1 = new byte[] { 0, 1, 2 };
            var arr2 = new byte[] { 0, 1, 2 };
            Assert.AreEqual(0, arr1.CompareTo(arr2));
            arr1 = new byte[] { 0, 1 };
            Assert.AreEqual(-1, arr1.CompareTo(arr2));
            arr2 = new byte[] { 0 };
            Assert.AreEqual(1, arr1.CompareTo(arr2));
            arr2 = new byte[] { 0, 2 };
            Assert.AreEqual(-1, arr1.CompareTo(arr2));
            arr1 = new byte[] { 0, 3, 1 };
            Assert.AreEqual(1, arr1.CompareTo(arr2));
            Assert.AreEqual(0, Array.Empty<byte>().CompareTo(Array.Empty<byte>()));
            Assert.ThrowsException<ArgumentNullException>(() => arr1.CompareTo(null));
        }
    }
}

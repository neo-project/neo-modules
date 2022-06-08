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
            ReadOnlySpan<byte> arr1 = new byte[] { 0, 1, 2 };
            ReadOnlySpan<byte> arr2 = new byte[] { 0, 1, 2 };
            Assert.AreEqual(0, arr1.CompareTo(arr2));
            arr1 = new byte[] { 0, 1 };
            Assert.AreEqual(-1, arr1.CompareTo(arr2));
            arr2 = new byte[] { 0 };
            Assert.AreEqual(1, arr1.CompareTo(arr2));
            arr2 = new byte[] { 0, 2 };
            Assert.AreEqual(-1, arr1.CompareTo(arr2));
            arr1 = new byte[] { 0, 3, 1 };
            Assert.AreEqual(1, arr1.CompareTo(arr2));
            Assert.AreEqual(0, ReadOnlySpan<byte>.Empty.CompareTo(ReadOnlySpan<byte>.Empty));
        }
    }
}

using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Listen;
using System.Collections.Generic;

namespace Neo.FileStorage.Tests.Listen
{
    [TestClass]
    public class UT_Helper : TestKit
    {
        [TestMethod]
        public void ParseToStringTest()
        {
            Assert.AreEqual("", new Dictionary<string, string>() { }.ParseToString());
            Assert.AreEqual("a=b", new Dictionary<string, string>()
            {
                { "a","b" }
            }
            .ParseToString());
            Assert.AreEqual("a=b&c=d", new Dictionary<string, string>()
            {
                { "a","b" },
                { "c","d" }
            }
            .ParseToString());
        }
    }
}

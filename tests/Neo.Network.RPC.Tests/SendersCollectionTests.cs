using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Plugins.Tests
{
    [TestClass()]
    public class SendersCollectionTests
    {
        [TestMethod()]
        public void FixedDictionaryTest()
        {
            SendersCollection dictionary = new SendersCollection(2);
            dictionary.Add(new ActorItem() { Hash = UInt256.Zero, Actor = null });
            dictionary.Add(new ActorItem() { Hash = UInt256.Parse("0x530de76326a8662d1b730ba4fbdf011051eabd142015587e846da42376adf35f"), Actor = null });
            dictionary.Add(new ActorItem() { Hash = UInt256.Parse("0x530de76326a8662d1b730ba4fbdf011051eabd142015587e846da42376adf350"), Actor = null });
            Assert.AreEqual(2, dictionary.Count);
        }
    }
}

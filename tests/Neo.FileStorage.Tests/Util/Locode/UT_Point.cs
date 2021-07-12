using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Utils.Locode.Column;
using Neo.FileStorage.Utils.Locode.Db;

namespace Neo.FileStorage.Tests.Util.Locode
{
    [TestClass]
    public class UT_Point
    {
        [TestMethod]
        public void TestPointFromCoordinates()
        {
            var c = Coordinates.CoordinatesFromString("4825N 01545E");
            var p = Point.PointFromCoordinates(c);
            Assert.AreEqual("48.25, 15.45", p.ToString());
        }
    }
}

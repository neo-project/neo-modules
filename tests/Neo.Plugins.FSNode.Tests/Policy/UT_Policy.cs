using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoFS.API.v2.Netmap;
using static Neo.FSNode.Policy.Helper;
using Sprache;

namespace Neo.Plugins.FSNode.Policy.Tests
{
    [TestClass]
    public class UT_Policy
    {
        [TestMethod]
        public void TestSimple()
        {
            string q = "REP 3";

            var expected = new PlacementPolicy(0, new Replica[] { new Replica(3, "") }, new Selector[0], new Filter[0]);
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestSimpleWithCBF()
        {
            string q = "REP 3 CBF 4";

            var expected = new PlacementPolicy(4, new Replica[] { new Replica(3, "") }, new Selector[0], new Filter[0]);
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestSelectFrom()
        {
            string q = @"REP 1 IN SPB
SELECT 1 IN City FROM * AS SPB";
            //string q = @"REP 1 IN SPB   SELECT 1 IN City FROM * AS SPB";

            var expected = new PlacementPolicy(0,
                new Replica[] { new Replica(1, "SPB") },
                new Selector[] { new Selector("SPB", "City", Clause.Unspecified, 1, "*") },
                new Filter[0]);
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestSelectFrom2()
        {
            string q = @"REP 1 IN SPB
SELECT 1 IN DISTINCT City FROM * AS SPB";

            var expected = new PlacementPolicy(0,
                new Replica[] { new Replica(1, "SPB") },
                new Selector[] { new Selector("SPB", "City", Clause.Distinct, 1, "*") },
                new Filter[0]);
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestSelectFromClause()
        {
            string q = @"REP 4
SELECT 3 IN Country FROM *
SELECT 2 IN SAME City FROM *
SELECT 1 IN DISTINCT Continent FROM *";

            var expected = new PlacementPolicy(0,
                new Replica[] { new Replica(4, "") },
                new Selector[]
                {
                    new Selector("", "Country", Clause.Unspecified, 3, "*"),
                    new Selector("", "City", Clause.Same, 2, "*"),
                    new Selector("", "Continent", Clause.Distinct, 1, "*")
                },
                new Filter[0]);
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));

            //Assert.AreEqual(expected.Selectors[0].Count, actual.Selectors[0].Count);
            //Assert.AreEqual(expected.Selectors[0].Clause, actual.Selectors[0].Clause); // Unspecified
            //Assert.AreEqual(expected.Selectors[1].Clause, actual.Selectors[1].Clause); // Same
            //Assert.AreEqual(expected.Selectors[1].Attribute, actual.Selectors[1].Attribute);
            //Assert.AreEqual(expected.Selectors[2].Clause, actual.Selectors[2].Clause); // Distinct
            //Assert.AreEqual(expected.Selectors[2].Filter, actual.Selectors[2].Filter);
            //Assert.AreEqual(expected.Selectors[2].Name, actual.Selectors[2].Name);
        }

        [TestMethod]
        public void TestSimpleFilter()
        {
            string q = @"REP 1
SELECT 1 IN City FROM Good
FILTER Rating GT 7 AS Good";

            var expected = new PlacementPolicy(0,
                new Replica[] { new Replica(1, "") },
                new Selector[] { new Selector("", "City", Clause.Unspecified, 1, "Good") },
                new Filter[] { new Filter("Good", "Rating", "7", Operation.Gt) });
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));

            //Assert.AreEqual(expected.Filters[0].Name, actual.Filters[0].Name);
            //Assert.AreEqual(expected.Filters[0].Key, actual.Filters[0].Key);
            //Assert.AreEqual(expected.Filters[0].Value, actual.Filters[0].Value);
            //Assert.AreEqual(expected.Filters[0].Op, actual.Filters[0].Op);
        }

        [TestMethod]
        public void TestFilterReference()
        {
            string q = @"REP 1
SELECT 2 IN City FROM Good
FILTER Country EQ RU AS FromRU
FILTER @FromRU AND Rating GT 7 AS Good";

            var expected = new PlacementPolicy(0,
                new Replica[] { new Replica(1, "") },
                new Selector[] { new Selector("", "City", Clause.Unspecified, 2, "Good") },
                new Filter[]
                {
                    new Filter("FromRU", "Country", "RU", Operation.Eq),
                    new Filter("Good", "", "", Operation.And,
                        new Filter(){ Name = "FromRU" },
                        new Filter("", "Rating", "7", Operation.Gt)
                    )
                });
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestFilterOps()
        {
            string q = @"REP 1
SELECT 2 IN City FROM Good
FILTER A GT 1 AND B GE 2 AND C LT 3 AND D LE 4
  AND E EQ 5 AND F NE 6 AS Good";

            var expected = new PlacementPolicy(0,
                new Replica[] { new Replica(1, "") },
                new Selector[] { new Selector("", "City", Clause.Unspecified, 2, "Good") },
                new Filter[]
                {
                    new Filter("Good", "", "", Operation.And,
                        new Filter("", "A", "1", Operation.Gt),
                        new Filter("", "B", "2", Operation.Ge),
                        new Filter("", "C", "3", Operation.Lt),
                        new Filter("", "D", "4", Operation.Le),
                        new Filter("", "E", "5", Operation.Eq),
                        new Filter("", "F", "6", Operation.Ne)
                    )
                });
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestFilterWithPrecedence()
        {
            string q = @"REP 7 IN SPB
SELECT 1 IN City FROM SPBSSD AS SPB
FILTER City EQ SPB AND SSD EQ true OR City EQ SPB AND Rating GE 5 AS SPBSSD";

            var expected = new PlacementPolicy(0,
               new Replica[] { new Replica(7, "SPB") },
               new Selector[] { new Selector("SPB", "City", Clause.Unspecified, 1, "SPBSSD") },
               new Filter[]
               {
                    new Filter("SPBSSD", "", "", Operation.Or,
                        new Filter("", "", "", Operation.And,
                            new Filter("", "City", "SPB", Operation.Eq),
                            new Filter("", "SSD", "true", Operation.Eq)
                        ),
                        new Filter("", "", "", Operation.And,
                            new Filter("", "City", "SPB", Operation.Eq),
                            new Filter("", "Rating", "5", Operation.Ge)
                        )
                    )
               });
            var actual = ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestValidation()
        {
            string q = @"REP 3 IN RU";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });

            q = @"REP 3
SELECT 1 IN City FROM MissingFilter";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });

            q = @"REP 3
SELECT 1 IN City FROM F
FILTER Country KEK RU AS F";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });

            q = @"REK 3";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });

            q = @"REP 3
SELECT 1 IN City FROM F
FILTER Good AND Country EQ RU AS F
FILTER Rating EQ 5 AS Good";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });

            q = @"REP 0";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });

            q = @"REP 1 IN Good
SELECT 0 IN City FROM *";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = ParsePlacementPolicy(q);
            });
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Fs.Netmap;
using Sprache;
using System;

namespace Neo.Fs.Policy.Tests
{
    [TestClass]
    public class UT_Policy
    {
        [TestMethod]
        public void TestSimple()
        {
            string q = "REP 3";

            var expected = new PlacementPolicy(0, new Replica[] { new Replica(3, "") }, new Selector[0], new Filter[0]);
            var actual = Helper.ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestSimpleWithCBF()
        {
            string q = "REP 3 CBF 4";

            var expected = new PlacementPolicy(4, new Replica[] { new Replica(3, "") }, new Selector[0], new Filter[0]);
            var actual = Helper.ParsePlacementPolicy(q);

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
                new Selector[] { new Selector(1, Clause.UnspecifiedClause, "City", "*", "SPB") },
                new Filter[0]);
            var actual = Helper.ParsePlacementPolicy(q);

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
                    new Selector(3, Clause.UnspecifiedClause, "Country", "*", ""),
                    new Selector(2, Clause.Same, "City", "*", ""),
                    new Selector(1, Clause.Distinct, "Continent", "*", "")
                },
                new Filter[0]);
            var actual = Helper.ParsePlacementPolicy(q);

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
                new Selector[] { new Selector(1, Clause.UnspecifiedClause, "City", "Good", "") },
                new Filter[] { new Filter("Good", "Rating", "7", Operation.GT) });
            var actual = Helper.ParsePlacementPolicy(q);

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
                new Selector[] { new Selector(2, Clause.UnspecifiedClause, "City", "Good", "") },
                new Filter[]
                {
                    new Filter("FromRU", "Country", "RU", Operation.EQ),
                    new Filter("Good", "", "", Operation.AND,
                        new Filter("FromRU"),
                        new Filter("", "Rating", "7", Operation.GT)
                    )
                });
            var actual = Helper.ParsePlacementPolicy(q);

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
                new Selector[] { new Selector(2, Clause.UnspecifiedClause, "City", "Good", "") },
                new Filter[]
                {
                    new Filter("Good", "", "", Operation.AND,
                        new Filter("", "A", "1", Operation.GT),
                        new Filter("", "B", "2", Operation.GE),
                        new Filter("", "C", "3", Operation.LT),
                        new Filter("", "D", "4", Operation.LE),
                        new Filter("", "E", "5", Operation.EQ),
                        new Filter("", "F", "6", Operation.NE)
                    )
                });
            var actual = Helper.ParsePlacementPolicy(q);

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
               new Selector[] { new Selector(1, Clause.UnspecifiedClause, "City", "SPBSSD", "SPB") },
               new Filter[]
               {
                    new Filter("SPBSSD", "", "", Operation.OR,
                        new Filter("", "", "", Operation.AND,
                            new Filter("", "City", "SPB", Operation.EQ),
                            new Filter("", "SSD", "true", Operation.EQ)
                        ),
                        new Filter("", "", "", Operation.AND,
                            new Filter("", "City", "SPB", Operation.EQ),
                            new Filter("", "Rating", "5", Operation.GE)
                        )
                    )
               });
            var actual = Helper.ParsePlacementPolicy(q);

            Assert.IsTrue(expected.Equals(actual));
        }

        [TestMethod]
        public void TestValidation()
        {
            string q = @"REP 3 IN RU";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });

            q = @"REP 3
SELECT 1 IN City FROM MissingFilter";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });

            q = @"REP 3
SELECT 1 IN City FROM F
FILTER Country KEK RU AS F";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });

            q = @"REK 3";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });

            q = @"REP 3
SELECT 1 IN City FROM F
FILTER Good AND Country EQ RU AS F
FILTER Rating EQ 5 AS Good";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });

            q = @"REP 0";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });

            q = @"REP 1 IN Good
SELECT 0 IN City FROM *";
            Assert.ThrowsException<ParseException>(() =>
            {
                var p = Helper.ParsePlacementPolicy(q);
            });
        }
    }
}

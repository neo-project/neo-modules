using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Neo.Plugins.RpcServer.Tests
{
    [TestClass]
    public class UT_RpcError
    {
        [TestMethod]
        public void AllDifferent()
        {
            HashSet<string> codes = new();

            foreach (RpcError error in typeof(RpcError)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(u => u.DeclaringType == typeof(RpcError))
                .Select(u => u.GetValue(null))
                .Cast<RpcError>())
            {
                Assert.IsTrue(codes.Add(error.ToString()));

                if (error.Code == RpcError.WalletFeeLimit.Code)
                    Assert.IsNotNull(error.Data);
                else
                    Assert.IsNull(error.Data);
            }
        }

        [TestMethod]
        public void TestJson()
        {
            Assert.AreEqual("{\"code\":-400,\"message\":\"Access deniedstatic\"}", RpcError.AccessDenied.ToJson().ToString(false));
        }
    }
}

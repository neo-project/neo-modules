using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;

namespace SmartContract.UnitTests
{
    [TestClass]
    public class UT_SmartContract
    {
        SmartContractPlugin uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new SmartContractPlugin();
        }

        [TestMethod]
        public void TestHelloWorld()
        {

        }
    }
}

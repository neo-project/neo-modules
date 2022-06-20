using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Acl;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Acl
{
    [TestClass]
    public class UT_AclChecker
    {
        [TestMethod]
        public void TestCheckRequest()
        {
            var container = FSContainer.Parser.ParseJson("{ \"version\": { \"major\": 2, \"minor\": 8 }, \"ownerID\": { \"value\": \"NfggdIxvpGdFLs9B3xWON3bZi4PhzxTy6A==\" }, \"nonce\": \"qTEhRv6uRaC8PZfwGRDWTg==\", \"basicACL\": 1073741823, \"attributes\": [ { \"key\": \"CreatedAt\", \"value\": \"9/9/2021 3:34:13 AM\" } ], \"placementPolicy\": { \"replicas\": [ { \"count\": 2 } ], \"containerBackupFactor\": 1 } }");
            Assert.IsTrue(container.BasicAcl.SystemAllowed(Operation.Put));
            var basicAcl = container.BasicAcl;
            Assert.IsTrue(basicAcl.Sticky());
        }

        [TestMethod]
        public void TestSystemSticky()
        {
            RequestInfo req = new()
            {
                Request = new PutRequest
                {
                    Body = new()
                    {
                        Init = new()
                        {
                            Header = new()
                            {
                                OwnerId = RandomOwnerID(),
                            }
                        }
                    }
                },
                SenderKey = RandomPrivatekey().LoadPrivateKey().PublicKey(),
                Role = Role.System,
            };
            req.BasicAcl.SetSticky();
            AclChecker checker = new();
            Assert.IsTrue(checker.StickyBitCheck(req));
        }

        [TestMethod]
        public void TestOwnerIDOrPublicKeyEmpty()
        {
            var basic_acl = BasicAcl.PublicBasicRule;
            var key = RandomPrivatekey().LoadPrivateKey();
            RequestInfo CreateRequest(bool isSticky, bool withKey, bool withOwner)
            {
                if (isSticky)
                    basic_acl.SetSticky();
                else
                    basic_acl.ResetSticky();
                return new()
                {
                    Request = new PutRequest
                    {
                        Body = new()
                        {
                            Init = new()
                            {
                                Header = new()
                                {
                                    OwnerId = withOwner ? OwnerID.FromPublicKey(key.PublicKey()) : null,
                                }
                            }
                        }
                    },
                    BasicAcl = basic_acl,
                    SenderKey = withKey ? key.PublicKey() : null
                };
            }
            AclChecker checker = new();
            Assert.IsFalse(checker.StickyBitCheck(CreateRequest(true, false, false)));
            Assert.IsFalse(checker.StickyBitCheck(CreateRequest(true, true, false)));
            Assert.IsFalse(checker.StickyBitCheck(CreateRequest(true, false, true)));
            Assert.IsTrue(checker.StickyBitCheck(CreateRequest(false, false, false)));
            Assert.IsTrue(checker.StickyBitCheck(CreateRequest(false, true, false)));
            Assert.IsTrue(checker.StickyBitCheck(CreateRequest(false, false, true)));
            Assert.IsTrue(checker.StickyBitCheck(CreateRequest(false, true, true)));
        }
    }
}

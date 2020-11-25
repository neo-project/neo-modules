using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoCrypto = Neo.Cryptography.Crypto;
using Neo.Fs.LocalObjectStorage.Bucket;
using System;
using System.IO;
using System.Text;

namespace Neo.Fs.LocalObjectStorage.Tests
{
    [TestClass]
    public class UT_TreeBucket
    {
        private static Random random = new Random();

        private string PrepareTree(bool badFiles)
        {
            var name = new byte[32];
            var root = Path.Combine(Path.GetTempPath(), "TreeBucketTest" + DateTime.Now.ToTimestamp().ToString());

            var paths = new string[][]
            {
                new string[]{root, "abcd"},
                new string[]{root, "abcd", "cdef"},
                new string[]{root, "abcd", "cd01"},
                new string[]{root, "0123", "2345"},
                new string[]{root, "0123", "2345", "4567"},
            };

            var dirs = new string[paths.Length];

            for (int i = 0; i < paths.Length; i++)
            {
                var t = Path.Combine(paths[i]);
                dirs[i] = t;
                Directory.CreateDirectory(dirs[i]);
                for (int j = 0; j < 2; j++)
                {
                    random.NextBytes(name);
                    var filePrefix = new StringBuilder();
                    for (int k = 1; k < paths[i].Length; k++)
                    {
                        filePrefix.Append(paths[i][k]);
                    }
                    filePrefix.Append(name.ToHexString());
                    var fileName = Path.Combine(dirs[i], filePrefix.ToString());
                    var file = File.Create(fileName);
                    file.Close();
                }

                if (!badFiles)
                    continue;
                // create one bad file
                random.NextBytes(name);
                var badFile = File.Create(Path.Combine(dirs[i], "fff" + name.ToHexString()));
                badFile.Close();
            }
            return root;
        }

        [TestMethod]
        public void TestTreeBucketList()
        {
            var root = PrepareTree(true);

            try
            {
                var b = new TreeBucket()
                {
                    Dir = root,
                    Perm = 0700,
                    Depth = 1,
                    PrefixLength = 4
                };
                var results = b.List();
                Assert.AreEqual(2, results.Length);

                b.Depth = 2;
                results = b.List();
                Assert.AreEqual(6, results.Length);

                b.Depth = 3;
                results = b.List();
                Assert.AreEqual(2, results.Length);

                b.Depth = 4;
                results = b.List();
                Assert.AreEqual(0, results.Length);
            }
            catch(Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (new DirectoryInfo(root).Exists)
                    Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void TestTreeBucket()
        {
            var root = PrepareTree(true);

            try
            {
                var b = new TreeBucket()
                {
                    Dir = root,
                    Perm = 0700,
                    Depth = 2,
                    PrefixLength = 4,
                    Sz = 0
                };

                var results = b.List();
                Assert.AreEqual(6, results.Length);

                // get
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.IsNotNull(b.Get(results[i]));
                }
                Assert.ThrowsException<ArgumentException>(() => b.Get(Encoding.ASCII.GetBytes("Hello world!")));

                // has
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.IsTrue(b.Has(results[i]));
                }
                Assert.IsFalse(b.Has(Encoding.ASCII.GetBytes("Unknown key")));

                // set sha256 key
                var key = NeoCrypto.Hash256(Encoding.ASCII.GetBytes("Set this key"));
                var value = new byte[32];
                random.NextBytes(value);
                b.Set(key, value);
                Assert.IsTrue(b.Has(key));
                var actual = b.Get(key);
                Assert.AreEqual(value.ToHexString(), actual.ToHexString());
                var fileName = key.ToHexString();
                var info = new FileInfo(Path.Combine(root, fileName[0..4], fileName[4..8], fileName));
                Assert.AreEqual(fileName, info.Name);

                // set key that cannot be placed in the required dir depth
                key = "abcdef".HexToBytes();
                Assert.ThrowsException<ArgumentException>(() => b.Set(key, value));

                // delete
                key = NeoCrypto.Hash256(Encoding.ASCII.GetBytes("Delete this key"));
                random.NextBytes(value);
                b.Set(key, value);
                Assert.IsTrue(b.Has(key));
                b.Del(key);
                Assert.IsFalse(b.Has(key));
                fileName = key.ToHexString();
                info = new FileInfo(Path.Combine(root, fileName[0..4], fileName[4..8], fileName));
                Assert.ThrowsException<FileNotFoundException>(() => { var l = info.Length; });
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (new DirectoryInfo(root).Exists)
                    Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void TestTreeBucketClose()
        {
            var root = PrepareTree(true);
            try
            {
                var b = new TreeBucket()
                {
                    Dir = root,
                    Perm = 0700,
                    Depth = 2,
                    PrefixLength = 4
                };

                b.Close();
                var info = new DirectoryInfo(root);
                Assert.IsFalse(info.Exists);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (new DirectoryInfo(root).Exists)
                    Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void TestTreeBucketSize()
        {
            var root = PrepareTree(true);

            var size = 1024;
            var value = new byte[size];
            random.NextBytes(value);

            try
            {
                var b = new TreeBucket()
                {
                    Dir = root,
                    Perm = 0700,
                    Depth = 2,
                    PrefixLength = 4,
                    Sz = 0
                };
                var key = Encoding.ASCII.GetBytes("Set this key");
                b.Set(key,value);
                Assert.AreEqual((long)size, b.Size());
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (new DirectoryInfo(root).Exists)
                    Directory.Delete(root, true);
            }
        }
    }
}

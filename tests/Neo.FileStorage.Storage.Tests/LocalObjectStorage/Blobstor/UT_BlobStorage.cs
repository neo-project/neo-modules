using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using System;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Tests.LocalObjectStorage.Blobstor
{
    [TestClass]
    public class UT_BlobStorage
    {
        private void RunTestCase(FSObject obj, bool compress, string[] excludeTypes, bool expect)
        {
            var stor = new BlobStorage(nameof(TestNeedsToCompress), new BlobStorageSettings
            {
                Compress = compress,
                CompressExcludeContentTypes = excludeTypes,
                BlobovniczasSettings = BlobovniczasSettings.Default,
                FSTreeSettings = FSTreeSettings.Default,
            });
            Assert.AreEqual(expect, stor.NeedsToCompress(obj));
            stor.Dispose();
        }

        [TestMethod]
        public void TestNeedsToCompress()
        {
            var obj = new FSObject
            {
                Header = new()
            };
            obj.Header.Attributes.Add(new Header.Types.Attribute { Key = Header.Types.Attribute.AttributeContentType, Value = "video/mpeg" });
            obj.Header.Attributes.Add(new Header.Types.Attribute { Key = Header.Types.Attribute.AttributeContentType, Value = "plain/text" });
            RunTestCase(obj, false, Array.Empty<string>(), false);
            RunTestCase(obj, false, new string[] { "video/*" }, false);
            RunTestCase(obj, false, new string[] { "audio/*" }, false);
            RunTestCase(obj, true, Array.Empty<string>(), true);
            RunTestCase(obj, true, new string[] { "video/*" }, false);
            RunTestCase(obj, true, new string[] { "audio/*" }, true);
            RunTestCase(obj, true, new string[] { "video/mpeg" }, false);
            RunTestCase(obj, true, new string[] { "*/mpeg" }, false);
            RunTestCase(obj, true, new string[] { "*" }, false);
        }
    }
}

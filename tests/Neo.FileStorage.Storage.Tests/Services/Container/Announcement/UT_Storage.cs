using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.Services.Container.Announcement.Storage;
using static Neo.FileStorage.Storage.Services.Container.Announcement.Storage.Helper;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Tests.Services.Container.Announcement
{
    [TestClass]
    public class UT_Storage
    {
        [TestMethod]
        public void TestFinalEstimation()
        {
            List<ulong> size = new() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Assert.AreEqual(4ul, FinalEstimation(size));
        }

        [TestMethod]
        public void TestStorageSameEpoch()
        {
            const ulong epoch = 13;
            var a1 = RandomAnnouncement();
            a1.Epoch = epoch;
            var a2 = RandomAnnouncement();
            a2.ContainerId = a1.ContainerId;
            a2.Epoch = epoch;
            AnnouncementStorage storage = new();
            storage.Put(a1);
            storage.Put(a2);
            int count = 0;
            storage.Iterate(a => a.Epoch == epoch, a =>
            {
                count++;
                Assert.AreEqual(FinalEstimation(new() { a1.UsedSpace, a2.UsedSpace }), a.UsedSpace);
            });
            Assert.AreEqual(1, count);
            var a3 = RandomAnnouncement();
            a3.Epoch = epoch;
            storage.Put(a3);
            count = 0;
            storage.Iterate(a => a.Epoch == epoch, a =>
            {
                count++;
                if (a.ContainerId.Equals(a1.ContainerId))
                {
                    Assert.AreEqual(FinalEstimation(new() { a1.UsedSpace, a2.UsedSpace }), a.UsedSpace);
                }
                else if (a.ContainerId.Equals(a3.ContainerId))
                {
                    Assert.AreEqual(a3.UsedSpace, a.UsedSpace);
                }
                else
                {
                    Assert.Fail();
                }
            });
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public void TestStorageDiffEpoch()
        {
            const ulong epoch1 = 13;
            const ulong epoch2 = 14;
            var a1 = RandomAnnouncement();
            a1.Epoch = epoch1;
            var a2 = RandomAnnouncement();
            a2.ContainerId = a1.ContainerId;
            a2.Epoch = epoch2;
            AnnouncementStorage storage = new();
            storage.Put(a1);
            storage.Put(a2);
            int count = 0;
            storage.Iterate(a => a.Epoch == epoch1, a =>
            {
                count++;
                Assert.AreEqual(a1.UsedSpace, a.UsedSpace);
            });
            Assert.AreEqual(1, count);
            count = 0;
            storage.Iterate(a => a.Epoch == epoch2, a =>
            {
                count++;
                Assert.AreEqual(a2.UsedSpace, a.UsedSpace);
            });
            Assert.AreEqual(1, count);
            var a3 = RandomAnnouncement();
            a3.Epoch = epoch1;
            a3.ContainerId = a1.ContainerId;
            storage.Put(a3);
            count = 0;
            storage.Iterate(a => a.Epoch == epoch1, a =>
            {
                count++;
                if (a.ContainerId.Equals(a1.ContainerId))
                {
                    Assert.AreEqual(FinalEstimation(new() { a1.UsedSpace, a3.UsedSpace }), a.UsedSpace);
                }
                else
                {
                    Assert.Fail();
                }
            });
            Assert.AreEqual(1, count);
        }
    }
}

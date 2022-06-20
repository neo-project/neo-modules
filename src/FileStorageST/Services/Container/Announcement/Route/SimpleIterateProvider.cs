using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using System;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Container.Announcement
{
    public class SimpleIterateProvider : IIteratorProvider
    {
        private readonly IIterator iterator;

        public SimpleIterateProvider(IIterator i)
        {
            iterator = i;
        }

        public IIterator InitIterator(CancellationToken cancellation)
        {
            if (iterator is null) throw new InvalidOperationException($"{nameof(SimpleIterateProvider)} no iterator");
            return iterator;
        }
    }
}

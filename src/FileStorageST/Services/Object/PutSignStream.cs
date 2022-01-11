using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Storage.Services.Object
{
    public sealed class PutSignStream : IRequestStream
    {
        public RequestSignStream Stream { get; init; }

        public bool Send(IRequest request)
        {
            return Stream.Send(request);
        }

        public IResponse Close()
        {
            return Stream.Close();
        }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }
}

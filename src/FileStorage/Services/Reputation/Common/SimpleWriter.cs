namespace Neo.FileStorage.Services.Reputaion.Common
{
    public class NonWriter : IWriter
    {
        public void Write(Trust _) { }

        public void Close() { }
    }
}

namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IWriter
    {
        void Write(Trust trust);

        void Close();
    }
}

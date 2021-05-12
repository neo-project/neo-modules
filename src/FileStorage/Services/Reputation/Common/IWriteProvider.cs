namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IWriterProvider
    {
        IWriter InitWriter(ICommonContext context);
    }
}

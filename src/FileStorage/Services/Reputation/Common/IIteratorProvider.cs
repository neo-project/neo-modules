namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IIteratorProvider
    {
        IIterator InitIterator(ICommonContext context);
    }
}

namespace Neo.FileStorage.InnerRing.Processors
{
    public interface IIndexer
    {
        int Index();
        void SetIndexer(int index);
        int InnerRingSize();
    }
}

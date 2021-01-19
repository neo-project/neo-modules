namespace Neo.Plugins.FSStorage.innerring.processors
{
    public interface IIndexer
    {
        int Index();
        void SetIndexer(int index);
        int InnerRingSize();
    }
}

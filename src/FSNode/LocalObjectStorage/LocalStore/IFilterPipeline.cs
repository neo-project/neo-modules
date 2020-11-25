using System;

namespace Neo.Fs.LocalObjectStorage.LocalStore
{
    public interface IFilterPipeline : IComparable<IFilterPipeline>
    {
        FilterResult Pass(WrapperContext ctx, ObjectMeta meta);
        void PutSubFilter(SubFilterParam Param);
        ulong GetPriority();
        void SetPriority(ulong value);
        string GetName();
    }
}

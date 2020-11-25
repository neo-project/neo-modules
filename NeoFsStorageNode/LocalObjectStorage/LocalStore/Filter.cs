using System;
using System.Threading;

namespace Neo.Fs.LocalObjectStorage.LocalStore
{
    public delegate FilterResult FilterFunc(WrapperContext ctx, ObjectMeta meta); // TBD, context

    public class WrapperContext
    {
        public HostExecutionContext Mock { get; set; }
    }

    public class FilterParam
    {
        public string Name { get; set; }
        public ulong Priority { get; set; }
        public FilterFunc FilterFunc { get; set; }
    }

    public class SubFilterParam
    {
        public PriorityFlag PriorityFlag { get; set; } //
        public IFilterPipeline FilterPipeline { get; set; } //
        public FilterCode OnIgnore { get; set; }
        public FilterCode OnPass { get; set; }
        public FilterCode OnFail { get; set; }
    }

    public class FilterResult // TBD
    {
        public FilterCode C { get; set; }
        public Exception E { get; set; } 

        public FilterResult(FilterCode c, Exception e = null)
        {
            this.C = c;
            this.E = e;
        }

        public static readonly FilterResult FrUndefined = new FilterResult(FilterCode.CodeUndefined);
        public static readonly FilterResult FrPass = new FilterResult(FilterCode.CodePass);
        public static readonly FilterResult FrFail = new FilterResult(FilterCode.CodeFail);
        public static readonly FilterResult FrIgnore = new FilterResult(FilterCode.CodeIgnore);

    }

    public enum PriorityFlag
    {
        PriorityValue = 0,
        PriorityMax = 1,
        PriorityMin = 2,
    }

    public enum FilterCode
    {
        CodeUndefined = 0,
        CodePass = 1,
        CodeFail = 2,
        CodeIgnore = 3,
    }

}

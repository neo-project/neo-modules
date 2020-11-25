using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Fs.LocalObjectStorage.LocalStore
{
    public class FilterPipeline : IFilterPipeline
    {
        public string Name { get; set; }
        public ulong Pri { get; set; }
        public FilterFunc FilterFn { get; set; }
        public ulong MaxSubPri { get; set; }
        public Dictionary<string, Dictionary<FilterCode, FilterCode>> MSubResult { get; set; }
        public IFilterPipeline[] SubFilters { get; set; }

        public FilterPipeline(FilterParam p)
        {
            this.Name = p.Name;
            this.Pri = p.Priority;
            this.FilterFn = p.FilterFunc;
            this.MSubResult = new Dictionary<string, Dictionary<FilterCode, FilterCode>>();
        }

        public FilterResult Pass(WrapperContext ctx, ObjectMeta meta)
        {
            foreach (var subFilter in this.SubFilters)
            {
                var subResult = subFilter.Pass(ctx, meta);
                var subName = subFilter.GetName();
                var subCode = subResult.C;

                if (subCode <= FilterCode.CodeUndefined)
                    return FilterResult.FrUndefined;

                var cFin = this.MSubResult[subName][subCode];
                if (cFin != FilterCode.CodeIgnore)
                    return new FilterResult(cFin, subResult.E);
            }
            if (this.FilterFn == null)
                return FilterResult.FrUndefined;

            return this.FilterFn(ctx, meta);
        }

        public void PutSubFilter(SubFilterParam param)
        {
            if (param.FilterPipeline == null)
                throw new Exception("could not put sub filter: empty filter pipeline");

            var name = param.FilterPipeline.GetName();
            if (this.MSubResult.ContainsKey(name))
                throw new Exception(string.Format("filter {0} is already in pipeline {1}", name, this.GetName()));

            if (param.PriorityFlag != PriorityFlag.PriorityMin)
            {
                var pri = param.FilterPipeline.GetPriority();
                if (pri < ulong.MaxValue)
                    param.FilterPipeline.SetPriority(pri + 1);
            }
            else
            {
                param.FilterPipeline.SetPriority(0);
            }
                
            switch (param.PriorityFlag)
            {
                case PriorityFlag.PriorityMax:
                    if (this.MaxSubPri < ulong.MaxValue)
                        this.MaxSubPri++;
                    param.FilterPipeline.SetPriority(this.MaxSubPri);
                    break;
                case PriorityFlag.PriorityValue:
                    var pri = param.FilterPipeline.GetPriority();
                    if (pri > this.MaxSubPri)
                        this.MaxSubPri = pri;
                    break;
                default:
                    break;
            }

            if (param.OnFail <= 0)
                param.OnFail = FilterCode.CodeIgnore;
            if (param.OnIgnore <= 0)
                param.OnIgnore = FilterCode.CodeIgnore;
            if (param.OnPass <= 0)
                param.OnPass = FilterCode.CodeIgnore;

            this.MSubResult[name] = new Dictionary<FilterCode, FilterCode>
            {
                {FilterCode.CodePass, param.OnPass },
                {FilterCode.CodeIgnore, param.OnIgnore },
                {FilterCode.CodeFail, param.OnFail }
            };

            this.SubFilters = this.SubFilters.Append(param.FilterPipeline).ToArray();
            Array.Sort(this.SubFilters);
        }

        public ulong GetPriority()
        {
            return this.Pri;
        }

        public void SetPriority(ulong value)
        {
            this.Pri = value;
        }

        public string GetName()
        {
            if (string.IsNullOrEmpty(this.Name))
                return "FILTER_UNNAMED";
            return this.Name;
        }

        public int CompareTo(IFilterPipeline other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            return this.GetPriority().CompareTo(other.GetPriority()) * (-1); // func (f filterPipelineSet) Less(i, j int) bool { return f[i].GetPriority() > f[j].GetPriority() }
        }
    }
}

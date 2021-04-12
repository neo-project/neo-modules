using Neo.FileStorage.API.Acl;
using Neo.FileStorage.Core.Container;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Services.Object.Acl.EAcl
{
    public class EAclValidator
    {
        private readonly IEAclSource EAclSource;

        public EAclValidator(IEAclSource source)
        {
            EAclSource = source;
        }

        public Action CalculateAction(ValidateUnit unit)
        {
            EACLTable table;
            if (unit.Bearer is null)
                table = unit.Bearer.Body?.EaclTable;
            else
                table = EAclSource.GetEACL(unit.Cid);
            if (table is null)
                return Action.Allow;
            return TableAction(unit, table);
        }

        private Action TableAction(ValidateUnit unit, EACLTable table)
        {
            foreach (var record in table.Records)
            {
                if (record.Operation != unit.Op)
                    continue;
                if (!TargetMatches(unit, record))
                    continue;
                var val = MatchFilters(unit.HeaderSource, record.Filters.ToList());
                if (val < 0) return Action.Allow;
                if (val == 0) return record.Action;
            }
            return Action.Allow;
        }

        private bool TargetMatches(ValidateUnit unit, EACLRecord record)
        {
            foreach (var target in record.Targets)
            {
                foreach (var key in target.Keys)
                {
                    if (key.SequenceEqual(unit.Key)) return true;
                }
                if (unit.Role == target.Role) return true;
            }
            return false;
        }

        private int MatchFilters(ITypedHeaderSource headerSource, List<EACLRecord.Types.Filter> filters)
        {
            int matched = 0;
            foreach (var filter in filters)
            {
                var headers = headerSource.HeadersOfSource(filter.HeaderType);
                if (headers is null) return -1;

                foreach (var header in headers)
                {
                    if (header is null) continue;
                    if (header.Key != filter.Key) continue;
                    switch (filter.MatchType)
                    {
                        case MatchType.StringEqual:
                            if (header.Value != filter.Value) continue;
                            break;
                        case MatchType.StringNotEqual:
                            if (header.Value == filter.Value) continue;
                            break;
                        default:
                            continue;
                    }
                    matched++;
                    break;
                }
            }
            return filters.Count - matched;
        }
    }
}
using Neo.FileStorage.API.Acl;
using System;
using System.Collections.Generic;
using System.Linq;
using AclAction = Neo.FileStorage.API.Acl.Action;

namespace Neo.FileStorage.Storage.Services.Object.Acl.EAcl
{
    public class EAclValidator
    {
        public IEACLSource EAclStorage { get; init; }

        public AclAction CalculateAction(ValidateUnit unit)
        {
            EACLTable table;
            if (unit.Bearer is not null)
                table = unit.Bearer.Body.EaclTable;
            else
            {
                try
                {
                    table = EAclStorage.GetEACL(unit.ContainerId);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(EAclValidator), LogLevel.Warning, $"couldn't get eacl, using ALLOW, error={e.Message}");
                    return AclAction.Allow;
                }
            }
            return TableAction(unit, table);
        }

        private AclAction TableAction(ValidateUnit unit, EACLTable table)
        {
            foreach (var record in table.Records)
            {
                if (record.Operation != unit.Op)
                    continue;
                if (!TargetMatches(unit, record))
                    continue;
                var val = MatchFilters(unit.HeaderSource, record.Filters);
                if (val < 0) return AclAction.Allow;
                if (val == 0) return record.Action;
            }
            return AclAction.Allow;
        }

        private bool TargetMatches(ValidateUnit unit, EACLRecord record)
        {
            foreach (var target in record.Targets)
            {
                if (target.Keys.Count > 0)
                {
                    foreach (var key in target.Keys)
                    {
                        if (key.SequenceEqual(unit.Key)) return true;
                    }
                    continue;
                }
                if (unit.Role == target.Role) return true;
            }
            return false;
        }

        private int MatchFilters(IHeaderSource headerSource, IEnumerable<EACLRecord.Types.Filter> filters)
        {
            int matched = 0;
            foreach (var filter in filters)
            {
                var headers = headerSource.HeadersOfType(filter.HeaderType);
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
            return filters.Count() - matched;
        }
    }
}
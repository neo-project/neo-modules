using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Search;
using System;

namespace Neo.FSNode.Services.Object.Head
{
    public class HeadService
    {
        private RelationSearcher relationSearcher;

        public HeadService(RelationSearcher relation_searcher)
        {
            relationSearcher = relation_searcher;
        }

        public HeadResult Head(HeadPrm prm)
        {
            var distribute_header = new DistributedHeader();
            var res = distribute_header.Head(prm);
            if (res != null || prm.Local) return res;
            var oid = relationSearcher.SearchRelation(prm.Address, prm);
            var address = new Address
            {
                ContainerId = prm.Address.ContainerId,
                ObjectId = oid,
            };
            var right_child_prm = new HeadPrm
            {
                Address = address,
            };
            right_child_prm.WithCommonPrm(prm);
            res = Head(right_child_prm);
            if (res is null) throw new InvalidOperationException(nameof(Head) + " could not get right child header");
            return res;
        }
    }
}
using Sprache;
using System;

namespace Neo.Fs.Policy
{
    public class Query
    {
        public ReplicaStmt[] Replicas { get; set; }
        public uint CBF { get; set; }
        public SelectorStmt[] Selectors { get; set; }
        public FilterStmt[] Filters { get; set; }

        public Query(ReplicaStmt[] replicas, uint cbf, SelectorStmt[] selectors, FilterStmt[] filters)
        {
            this.Replicas = replicas;
            this.CBF = cbf;
            this.Selectors = selectors;
            this.Filters = filters;
        }

        public static Query Parse(string s)
        {
            return Helper.QueryParser.Parse(s);
        }
    }

    public class ReplicaStmt
    {
        public uint Count { get; set; }
        public string Selector { get; set; }

        public ReplicaStmt(uint count, string selector)
        {
            this.Count = count;
            this.Selector = selector;
        }
    }

    public class SelectorStmt
    {
        public uint Count { get; set; }
        public string Clause { get; set; }
        public string Bucket { get; set; }
        public string Filter { get; set; }
        public string Name { get; set; }

        public SelectorStmt(uint count, string clause, string bucket, string filter, string name)
        {
            this.Count = count;
            this.Clause = clause;
            this.Bucket = bucket;
            this.Filter = filter;
            this.Name = name;
        }
    }

    public class FilterStmt
    {
        public OrChain Value { get; set; }
        public string Name { get; set; }

        public FilterStmt(OrChain value, string name)
        {
            this.Value = value;
            this.Name = name;
        }
    }

    public class OrChain
    {
        public AndChain[] Clauses { get; set; }

        public OrChain(AndChain[] clauses)
        {
            this.Clauses = clauses;
        }
    }

    public class AndChain
    {
        public FilterOrExpr[] Clauses { get; set; }

        public AndChain(FilterOrExpr[] clauses)
        {
            this.Clauses = clauses;
        }
    }

    public class FilterOrExpr
    {
        public string Reference { get; set; }
        public SimpleExpr Expr { get; set; }

        public FilterOrExpr(string value)
        {
            if (value.StartsWith("@"))
            {
                this.Reference = value[1..];
                this.Expr = null;
            }
            else
            {
                var values = value.Split(" ");
                if (values.Length != 3)
                    throw new ArgumentException();
                this.Expr = new SimpleExpr(values[0], values[1], values[2]);
                this.Reference = "";
            }
        }
    }

    public class SimpleExpr
    {
        public string Key { get; set; }
        public string Op { get; set; }
        public string Value { get; set; }

        public SimpleExpr(string key, string op, string value)
        {
            this.Key = key;
            this.Op = op;
            this.Value = value;
        }
    }
}

using System;

namespace Neo.Fs.Netmap
{
    public class Filter : IEquatable<Filter>
    {
        public const string MainFilterName = "*";
        public string Name { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public Operation Op { get; set; }
        public Filter[] Filters { get; set; }

        public Filter(string name, string key, string value, Operation op, params Filter[] filters)
        {
            this.Name = name;
            this.Key = key;
            this.Value = value;
            this.Op = op;
            this.Filters = filters;
        }

        public Filter(string name)
        {
            this.Name = name;
        }

        public override bool Equals(object other)
        {
            return other is Filter f && Equals(f);
        }

        public bool Equals(Filter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.Name != other.Name) return false;
            if (this.Key != other.Key) return false;
            if (this.Value != other.Value) return false;
            if (this.Op != other.Op) return false;
            if (!((this.Filters is null || this.Filters.Length == 0) && (other.Filters is null || other.Filters.Length == 0)))
            {
                if (this.Filters.Length != other.Filters.Length)
                    return false;
                for (int i = 0; i < this.Filters.Length; i++)
                {
                    if (!this.Filters[i].Equals(other.Filters[i]))
                        return false;
                }
            }
            return true;
        }
    }

    public enum Operation
    {
        UnspecifiedOperation = 0,
        EQ = 1,
        NE = 2,
        GT = 3,
        GE = 4,
        LT = 5,
        LE = 6,
        OR = 7,
        AND = 8
    }

    public class Selector : IEquatable<Selector>
    {
        public string Name { get; set; }
        public string Attribute { get; set; }
        public Clause Clause { get; set; }
        public uint Count { get; set; }
        public string Filter { get; set; }

        //public Selector(string name, string attribute, Clause clause, uint count, string filter)
        public Selector(uint count, Clause clause, string attribute, string filter, string name)
        {
            this.Name = name;
            this.Attribute = attribute;
            this.Clause = clause;
            this.Count = count;
            this.Filter = filter;
        }

        public bool Equals(Selector other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.Name != other.Name) return false;
            if (this.Attribute != other.Attribute) return false;
            if (this.Clause != other.Clause) return false;
            if (this.Count != other.Count) return false;
            if (this.Filter != other.Filter) return false;
            return true;
        }
    }

    public enum Clause
    {
        UnspecifiedClause = 0,
        Same = 1,
        Distinct = 2
    }

    public class Replica : IEquatable<Replica>
    {
        public uint Count { get; set; }
        public string Selector { get; set; }

        public Replica(uint count, string selector)
        {
            this.Count = count;
            this.Selector = selector;
        }

        public bool Equals(Replica other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.Count != other.Count) return false;
            if (this.Selector != other.Selector) return false;
            return true;
        }
    }

    public class PlacementPolicy : IEquatable<PlacementPolicy>
    {
        public uint ContainerBackupFactor { get; set; }
        public Replica[] Replicas { get; set; }
        public Selector[] Selectors { get; set; }
        public Filter[] Filters { get; set; }

        public PlacementPolicy(uint cbf, Replica[] replicas, Selector[] selectors, Filter[] filters)
        {
            this.ContainerBackupFactor = cbf;
            this.Replicas = replicas;
            this.Selectors = selectors;
            this.Filters = filters;
        }

        public bool Equals(PlacementPolicy other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.ContainerBackupFactor != other.ContainerBackupFactor)
                return false;
            if (!((this.Replicas is null || this.Replicas.Length == 0) && (other.Replicas is null || other.Replicas.Length == 0)))
            {
                if (this.Replicas.Length != other.Replicas.Length)
                    return false;
                for (int i = 0; i < this.Replicas.Length; i++)
                {
                    if (!this.Replicas[i].Equals(other.Replicas[i]))
                        return false;
                }
            }
            if (!((this.Selectors is null || this.Selectors.Length == 0) && (other.Selectors is null || other.Selectors.Length == 0)))
            {
                if (this.Selectors.Length != other.Selectors.Length)
                    return false;
                for (int i = 0; i < this.Selectors.Length; i++)
                {
                    if (!this.Selectors[i].Equals(other.Selectors[i]))
                        return false;
                }
            }
            if (!((this.Filters is null || this.Filters.Length == 0) && (other.Filters is null || other.Filters.Length == 0)))
            {
                if (this.Filters.Length != other.Filters.Length)
                    return false;
                for (int i = 0; i < this.Filters.Length; i++)
                {
                    if (!this.Filters[i].Equals(other.Filters[i]))
                        return false;
                }
            }
            return true;
        }
    }

    // Attribute of storage node
    public class Attribute
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string[] Parents { get; set; }
    }


    public class NodeInfo
    {
        public byte[] PublicKey { get; set; }
        public string Address { get; set; }
        public Attribute[] Attributes { get; set; }
        public NodeState State { get; set; }
    }

    public enum NodeState
    {
        UnspecifiedState = 0,
        Online = 1,
        Offline = 2
    }


}

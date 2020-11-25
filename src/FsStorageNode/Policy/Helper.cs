using Neo.Fs.Netmap;
using Neo.IO.Json;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Fs.Policy
{
    public static class Helper
    {
        public static Parser<string> BlankParser =
            from s in Parse.WhiteSpace.Many().Text().Or(Parse.LineEnd).Or(Parse.String("\t").Text())
            select s;

        public static Parser<uint> ReplFactorParser =
            from leading in BlankParser.Many()
            from RF in Parse.String("RF")
            from space in BlankParser.AtLeastOnce()
            from num in Parse.Number
            select uint.Parse(num);

        public static Parser<Operation> OperationParser =
            from space1 in BlankParser.Many()
            from op in Parse.AtLeastOnce(Parse.CharExcept(' '))
            select new string(op.ToArray()).ToOperation();

        public static Parser<Clause> ClauseParser =
            from space1 in BlankParser.Many()
            from clause in Parse.LetterOrDigit.AtLeastOnce()
            select new string(clause.ToArray()).ToClause();

        public static Parser<Filter> FilterParser =
            from space1 in BlankParser.Many()
            from name in Parse.LetterOrDigit.Many() // name, can be letters or ""
            from space in BlankParser.AtLeastOnce()
            from key in Parse.Letter.Many() // key, can only be letters or ""
            from space2 in BlankParser.AtLeastOnce()
            from value in Parse.AnyChar.Many() // value, can be any or ""
            from space3 in BlankParser.AtLeastOnce()
            from op in OperationParser // op
            from space4 in BlankParser.AtLeastOnce()
            from filters in FilterParser.XMany() // recursive call
            select new Filter(new string(name.ToArray()), new string(key.ToArray()), new string(value.ToArray()), op, filters.ToArray());

        public static Parser<Filter[]> FilterGroupParser =
            from leading in BlankParser.Many()
            from filter_str in Parse.String("FILTER")
            from filters in FilterParser.XAtLeastOnce()
            select filters.ToArray();

        //public static Parser<Selector> SelectorParser =
        //    from space1 in BlankParser.Many()
        //    from name in Parse.Letter.AtLeastOnce().Text() // name
        //    from space2 in BlankParser.AtLeastOnce()
        //    from attr in Parse.Letter.AtLeastOnce().Text() // attribute
        //    from space3 in BlankParser.AtLeastOnce()
        //    from clause in ClauseParser // clause
        //    from space4 in BlankParser.AtLeastOnce()
        //    from count in Parse.Number // count
        //    from space5 in BlankParser.AtLeastOnce()
        //    from filter in Parse.Letter.AtLeastOnce().Text() // filter name
        //    select new Selector(uint.Parse(count), clause,  attr, filter, name);

        //public static Parser<Selector[]> SelectorGroupParser =
        //    from space1 in BlankParser.Many()
        //    from selectors in SelectorParser.XAtLeastOnce()
        //    select selectors.ToArray();

        //public static Parser<Replica> ReplicaParser =
        //    from space1 in BlankParser.Many()
        //    from count in Parse.Number // count
        //    from space2 in BlankParser.AtLeastOnce()
        //    from selector in Parse.Letter.AtLeastOnce().Text() // selector name
        //    select new Replica(uint.Parse(count), selector);

        //public static Parser<Replica[]> ReplicaGroupParser =
        //    from space1 in BlankParser.Many()
        //    from replicas in ReplicaParser.XAtLeastOnce()
        //    select replicas.ToArray();

        //public static Parser<PlacementPolicy> PlacementPolicyParser =
        //    from space1 in BlankParser.Many()
        //    from bf in Parse.Number
        //    from replicas in ReplicaGroupParser
        //    from selectors in SelectorGroupParser
        //    from filters in FilterGroupParser
        //    select new PlacementPolicy(uint.Parse(bf), replicas, selectors, filters);

        public static Operation ToOperation(this string str)
        {
            var s = str.ToUpper();
            return s switch
            {
                "EQ" => Operation.EQ,
                "=" => Operation.EQ,
                "NE" => Operation.NE,
                "!=" => Operation.NE,
                "GT" => Operation.GT,
                ">" => Operation.GT,
                "GE" => Operation.GE,
                ">=" => Operation.GE,
                "LT" => Operation.LT,
                "<" => Operation.LT,
                "LE" => Operation.LE,
                "<=" => Operation.LE,
                "OR" => Operation.OR,
                "AND" => Operation.AND,
                _ => throw new ArgumentException(),
            };
        }

        public static string AsString(this Operation op)
        {
            return op switch
            {
                Operation.EQ => "EQ",
                Operation.NE => "NE",
                Operation.GT => "GT",
                Operation.GE => "GE",
                Operation.LT => "LT",
                Operation.LE => "LE",
                Operation.AND => "AND",
                Operation.OR => "OR",
                _ => throw new ArgumentException(),
            };
        }

        public static Clause ToClause(this string str)
        {
            var s = str.ToLower();
            return s switch
            {
                "unspecifiedclause" => Clause.UnspecifiedClause,
                "0" => Clause.UnspecifiedClause,
                "" => Clause.UnspecifiedClause,
                "same" => Clause.Same,
                "1" => Clause.Same,
                "distinct" => Clause.Distinct,
                "2" => Clause.Distinct,
                _ => throw new ArgumentException(),
            };
        }

        public static string AsString(this Clause clause)
        {
            return clause switch
            {
                Clause.Same => "SAME",
                Clause.Distinct => "DISTINCT",
                Clause.UnspecifiedClause => "",
                _ => throw new ArgumentException(),
            };
        }


        #region query
        public static Parser<char> Digit1Parser =                       // '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' 
            from leading in BlankParser.Many()
            from digit1 in Parse.Digit.Except(Parse.Char('0'))
            select digit1;

        public static Parser<uint> Number1Parser =                      // Digit1[Digit];
            from leading in BlankParser.Many()
            from n1 in Digit1Parser // cannot start with '0'
            from n in Parse.Digit.Many()
            select uint.Parse(new string(new char[] { n1 }.Concat(n).ToArray()));

        //public static Parser<uint> NumberParser =
        //    from leading in BlankParser.Many()
        //    from n in Parse.Digit.AtLeastOnce()
        //    select uint.Parse(n.ToArray());

        public static Parser<string> NumberParser =
            from leading in BlankParser.Many()
            from n in Parse.Digit.AtLeastOnce()
            select new string(n.ToArray());

        public static Parser<string> IdentParser =
            from leading in BlankParser.Many()
            from a1 in Parse.Letter.Once() // assuming identity only starts with letters
            from a in Parse.LetterOrDigit.Many()
            select new string(a1.Concat(a).ToArray());

        public static Parser<string> AtIdentParser =
            from leading in BlankParser.Many()
            from at in Parse.Char('@')
            from id in IdentParser
            select new string(at.ToString().Concat(id).ToArray());

        public static Parser<string> StringParser =
            from leading in BlankParser.Many()
            from s in Parse.AtLeastOnce(Parse.CharExcept(' '))
            select new string(s.ToArray());

        public static Parser<string> ValueParser =
            from leading in BlankParser.Many()
            from v in IdentParser.Or(NumberParser).Or(StringParser)
            select v;

        public static Parser<string> OpParser =
            from leading in BlankParser.Many()
            from op in Parse.String("EQ")
                .Or(Parse.String("NE"))
                .Or(Parse.String("GE"))
                .Or(Parse.String("GT"))
                .Or(Parse.String("LE"))
                .Or(Parse.String("LT"))
            select new string(op.ToArray());

        public static Parser<string> AttributeFilterParser =
            from leading in BlankParser.Many()
            from ident in IdentParser
            from space1 in BlankParser.AtLeastOnce()
            from op in OpParser
            from space2 in BlankParser.AtLeastOnce()
            from value in ValueParser
            select new string(ident.Concat(" ").Concat(op).Concat(" ").Concat(value).ToArray());

        public static Parser<SimpleExpr> SimpleExprParser =
            from leading in BlankParser.Many()
            from key in IdentParser
            from space1 in BlankParser.AtLeastOnce()
            from op in OpParser
            from space2 in BlankParser.AtLeastOnce()
            from value in ValueParser
            select new SimpleExpr(key, op, value);

        //Expr::=
        //    '@' Ident(* filter reference *)
        //  | Ident, Op, Value(* attribute filter *)
        //;
        public static Parser<FilterOrExpr> FilterOrExprParser =
            from leading in BlankParser.Many()
            from expr in AtIdentParser.Or(AttributeFilterParser)
            select new FilterOrExpr(expr);

        public static Parser<FilterOrExpr> AndExprParser =
           from leading in BlankParser.Many()
           from and in Parse.String("AND")
           from space1 in BlankParser.AtLeastOnce()
           from f in FilterOrExprParser
           select f;

        //AndChain::=
        //    Expr, ['AND', Expr]
        //;
        public static Parser<AndChain> AndChainParser =
            from leading in BlankParser.Many()
            from expr1 in FilterOrExprParser
            from space1 in BlankParser.AtLeastOnce()
            from exprs in AndExprParser.Many()
            select new AndChain(new FilterOrExpr[] { expr1 }.Concat(exprs).ToArray());

        public static Parser<AndChain> OrExprParser =
            from leading in BlankParser.Many()
            from or in Parse.String("OR")
            from space1 in BlankParser.AtLeastOnce()
            from a in AndChainParser
            select a;

        //OrChain::=
        //    AndChain, ['OR', AndChain]
        //;
        public static Parser<OrChain> OrChainParser =
            from leading in BlankParser.Many()
            from and1 in AndChainParser
            from space1 in BlankParser.AtLeastOnce()
            from ands in OrExprParser.Many()
            select new OrChain(new AndChain[] { and1 }.Concat(ands).ToArray());

        public static Parser<string> AsIdentParser =
            from leading in BlankParser.Many()
            from a in Parse.String("AS")
            from space1 in BlankParser.AtLeastOnce()
            from id in IdentParser
            select id;

        public static Parser<string> InIdentParser =
            from leading in BlankParser.Many()
            from a in Parse.String("IN")
            from space1 in BlankParser.AtLeastOnce()
            from id in IdentParser
            select id;

        //FilterStmt::=
        //    'FILTER', AndChain, ['OR', AndChain],
        //    'AS', Ident(* obligatory filter name*)
        //;
        public static Parser<FilterStmt> FilterStmtParser =
            from leading in BlankParser.Many()
            from filter in Parse.String("FILTER")
            from space1 in BlankParser.AtLeastOnce()
            from or in OrChainParser // should only have one OrChain for each filter
            from space2 in BlankParser.AtLeastOnce()
            from a in AsIdentParser
            select new FilterStmt(or, a);

        public static Parser<string> ClauseStringParser =
            from leading in BlankParser.Many()
            from c in Parse.String("SAME").Or(Parse.String("DISTINCT"))
            select new string(c.ToArray());

        //SelectStmt::=
        //    'SELECT', Number1,    (* number of nodes to select without container backup factor*)
        //    'IN', Clause?, Ident, (* bucket name *)
        //    FROM, (Ident | '*'),  (* filter reference or whole netmap*)
        //    ('AS', Ident)?        (* optional selector name*)
        //;
        public static Parser<SelectorStmt> SelectorStmtParser =
            from leading in BlankParser.Many()
            from selectStr in Parse.String("SELECT")
            from space1 in BlankParser.AtLeastOnce()
            from n1 in Number1Parser
            from space2 in BlankParser.AtLeastOnce()
            from inStr in Parse.String("IN")
            from space3 in BlankParser.AtLeastOnce()
            from clause in ClauseStringParser.Optional()
            from space4 in BlankParser.AtLeastOnce()
            from bucket in IdentParser
            from space5 in BlankParser.AtLeastOnce()
            from fromStr in Parse.String("FROM")
            from space6 in BlankParser.AtLeastOnce()
            from filter in IdentParser.Or(Parse.String("*")).Text()
            from name in AsIdentParser.Optional()
            select new SelectorStmt(n1, clause.GetOrElse(""), bucket, filter, name.GetOrElse(""));

        public static Parser<uint> CbfParser =
            from leading in BlankParser.Many()
            from cbf in Parse.String("CBF")
            from space1 in BlankParser.AtLeastOnce()
            from n in Number1Parser
            select n;

        public static Parser<ReplicaStmt> ReplicaStmtParser =
            from leading in BlankParser.Many()
            from rep in Parse.String("REP")
            from space1 in BlankParser.AtLeastOnce()
            from n1 in Number1Parser
            from space2 in BlankParser.Many()
            from selector in InIdentParser.Optional()
            select new ReplicaStmt(n1, selector.GetOrElse(""));

        public static Parser<Query> QueryParser =
            from leading in BlankParser.Many()
            from replicas in ReplicaStmtParser.AtLeastOnce()
            from space1 in BlankParser.Many()
            from cbf in CbfParser.Optional()
            from space2 in BlankParser.Many()
            from selectors in SelectorStmtParser.Many()
            from space3 in BlankParser.Many()
            from filters in FilterStmtParser.Many()
            select new Query(replicas.ToArray(), cbf.GetOrElse<uint>(0), selectors.ToArray(), filters.ToArray());
        #endregion


        public static PlacementPolicy ParsePlacementPolicy(string s)
        {
            var q = Query.Parse(s);

            var seenFilters = new Dictionary<string, bool>();
            var fs = new Filter[0];
            foreach (var qf in q.Filters)
            {
                var f = FilterFromOrChain(qf.Value, seenFilters);
                f.Name = qf.Name;
                fs = fs.Append(f).ToArray();
                seenFilters[qf.Name] = true;
            }

            var seenSelectors = new Dictionary<string, bool>();
            var ss = new Selector[0];
            foreach (var qs in q.Selectors)
            {
                if (qs.Filter != Filter.MainFilterName && (!seenFilters.ContainsKey(qs.Filter) || !seenFilters[qs.Filter]))
                    throw new ParseException("unknown filter");
                Selector selector = new Selector(qs.Count, qs.Clause.ToClause(), qs.Bucket, qs.Filter, qs.Name);
                seenSelectors[qs.Name] = true;
                ss = ss.Append(selector).ToArray();
            }

            var rs = new Replica[0];
            foreach (var qr in q.Replicas)
            {
                string selector = "";
                if (qr.Selector != "")
                {
                    if (!seenSelectors.ContainsKey(qr.Selector) || !seenSelectors[qr.Selector])
                        throw new ParseException("unknow selector");
                    selector = qr.Selector;
                }
                var r = new Replica(qr.Count, selector);
                rs = rs.Append(r).ToArray();
            }

            return new PlacementPolicy(q.CBF, rs, ss, fs);
        }


        public static Filter FilterFromOrChain(OrChain expr, Dictionary<string, bool> seen)
        {
            var fs = new Filter[] { };
            foreach (var ac in expr.Clauses)
            {
                var f = FilterFromAndChain(ac, seen);
                fs = fs.Append(f).ToArray();
            }
            if (fs.Length == 1)
                return fs[0];

            return new Filter("", "", "", Operation.OR, fs);
        }

        public static Filter FilterFromAndChain(AndChain expr, Dictionary<string, bool> seen)
        {
            var fs = new Filter[] { };
            foreach (var fe in expr.Clauses)
            {
                Filter f;
                if (fe.Expr != null)
                    f = FilterFromSimpleExpr(fe.Expr);
                else
                    f = new Filter(fe.Reference);
                fs = fs.Append(f).ToArray();
            }
            if (fs.Length == 1)
                return fs[0];
            return new Filter("", "", "", Operation.AND, fs);
        }

        public static Filter FilterFromSimpleExpr(SimpleExpr se)
        {
            return new Filter("", se.Key, se.Value, se.Op.ToOperation(), new Filter[] { });
        }

        // json.go
        public static JObject ToJson(this PlacementPolicy np)
        {
            JObject json = new JObject();
            json["replicas"] = np.Replicas.Select(p => p.ToJson()).ToArray();
            if (np.ContainerBackupFactor != 0)
                json["container_backup_factor"] = np.ContainerBackupFactor.ToString();
            if (np.Selectors != null && np.Selectors.Length != 0)
                json["selectors"] = np.Selectors.Select(p => p.ToJson()).ToArray();
            if (np.Filters != null && np.Filters.Length != 0)
                json["filters"] = np.Filters.Select(p => p.ToJson()).ToArray();
            return json;
        }

        public static PlacementPolicy PolicyFromJson(this JObject json)
        {
            return new PlacementPolicy(json.ContainsProperty("container_backup_factor") ? uint.Parse(json["container_backup_factor"].AsString()) : 0,
                (json["replicas"] as JArray).Select(p => ReplicaFromJson(p)).ToArray(),
                json.ContainsProperty("selectors") ? (json["selectors"] as JArray).Select(p => SelectorFromJson(p)).ToArray() : new Selector[] { },
                json.ContainsProperty("filters") ? (json["filters"] as JArray).Select(p => FilterFromJson(p)).ToArray() : new Filter[] { });
        }

        public static JObject ToJson(this Replica rep)
        {
            JObject json = new JObject();
            json["count"] = rep.Count.ToString();
            if (!string.IsNullOrEmpty(rep.Selector))
                json["selector"] = rep.Selector;
            return json;
        }

        public static Replica ReplicaFromJson(this JObject json)
        {
            return new Replica(uint.Parse(json["count"].AsString()),
                json["selector"].AsString());
        }

        public static JObject ToJson(this Selector selector)
        {
            JObject json = new JObject();
            json["count"] = selector.Count.ToString();
            json["attribute"] = selector.Attribute;
            //if (!string.IsNullOrEmpty(selector.Filter))
            json["filter"] = selector.Filter;
            if (!string.IsNullOrEmpty(selector.Name))
                json["name"] = selector.Name;
            if (!string.IsNullOrEmpty(selector.Clause.AsString()))
                json["clause"] = selector.Clause.AsString();
            return json;
        }

        public static Selector SelectorFromJson(this JObject json)
        {
            return new Selector(uint.Parse(json["count"].AsString()),
                json.ContainsProperty("clause") ? json["clause"].AsString().ToClause() : Clause.UnspecifiedClause,
                json["attribute"].AsString(),
                json["filter"].AsString(),
                json.ContainsProperty("name") ? json["name"].AsString() : "");
        }

        public static JObject ToJson(this Filter filter)
        {
            JObject json = new JObject();
            if (!string.IsNullOrEmpty(filter.Name))
                json["name"] = filter.Name;
            if (!string.IsNullOrEmpty(filter.Key))
                json["key"] = filter.Key;
            if (!string.IsNullOrEmpty(filter.Op.AsString()))
                json["op"] = filter.Op.AsString();
            if (!string.IsNullOrEmpty(filter.Value))
                json["value"] = filter.Value;
            if (filter.Filters != null && filter.Filters.Length != 0)
                json["filters"] = filter.Filters.Select(p => p.ToJson()).ToArray();
            return json;
        }

        public static Filter FilterFromJson(this JObject json)
        {
            return new Filter(json.ContainsProperty("name") ? json["name"].AsString() : "",
                json.ContainsProperty("key") ? json["key"].AsString() : "",
                json.ContainsProperty("value") ? json["value"].AsString() : "",
                json.ContainsProperty("op") ? json["op"].AsString().ToOperation() : Operation.UnspecifiedOperation,
                json.ContainsProperty("filters") ? (json["filters"] as JArray).Select(p => FilterFromJson(p)).ToArray() : null);
        }
    }
}

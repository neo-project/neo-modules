using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.InnerRing.Utils.Locode.Column;
using Neo.FileStorage.InnerRing.Utils.Locode.Db;

namespace Neo.FileStorage.InnerRing.Utils.Locode
{
    public class LocodeValidator
    {
        public class AttrDescriptor
        {
            public Func<(Key, Record), string> converter;
            public bool optional;
            public static string CountryCodeValue((Key, Record) record)
            {
                return string.Concat<char>(record.Item1.CountryCode.Symbols());
            }

            public static string CountryValue((Key, Record) record)
            {
                return record.Item2.CountryName;
            }

            public static string LocationCodeValue((Key, Record) record)
            {
                return string.Concat<char>(record.Item1.LocationCode.Symbols());
            }

            public static string LocationValue((Key, Record) record)
            {
                return record.Item2.LocationName;
            }

            public static string SubDivCodeValue((Key, Record) record)
            {
                return record.Item2.SubDivCode;
            }

            public static string SubDivValue((Key, Record) record)
            {
                return record.Item2.SubDivName;
            }

            public static string ContinentValue((Key, Record) record)
            {
                return record.Item2.Continent.String();
            }
        }

        private Dictionary<string, AttrDescriptor> mAttr;
        private StorageDB dB;

        public LocodeValidator(StorageDB dB)
        {
            this.dB = dB;
            this.mAttr = new Dictionary<string, AttrDescriptor>()
                {
                    { Node.AttributeCountryCode,new AttrDescriptor(){ converter=AttrDescriptor.CountryCodeValue} },
                    { Node.AttributeCountry,new AttrDescriptor(){ converter=AttrDescriptor.CountryValue} },
                    { Node.AttributeLocation,new AttrDescriptor(){ converter=AttrDescriptor.LocationValue} },
                    { Node.AttributeSubDivCode,new AttrDescriptor(){ converter=AttrDescriptor.SubDivCodeValue,optional=true} },
                    { Node.AttributeSubDiv,new AttrDescriptor(){ converter=AttrDescriptor.SubDivValue,optional=true} },
                    { Node.AttributeContinent,new AttrDescriptor(){ converter=AttrDescriptor.ContinentValue} },
                };
        }

        public void VerifyAndUpdate(API.Netmap.NodeInfo n)
        {
            var tAttr = UniqueAttributes(n.Attributes.GetEnumerator());
            if (!tAttr.TryGetValue(Node.AttributeUNLOCODE, out var attrLocode)) return;
            var lc = LOCODE.FromString(attrLocode.Value);
            (Key, Record) record = dB.Get(lc);
            foreach (var attr in mAttr)
            {
                var attrVal = attr.Value.converter(record);
                if (attrVal == "")
                {
                    if (!attr.Value.optional)
                        throw new Exception("missing required attribute in DB record");
                    continue;
                }
                API.Netmap.NodeInfo.Types.Attribute a = new();
                a.Key = attr.Key;
                a.Value = attrVal;
                tAttr[attr.Key] = a;
            }
            var ass = new List<API.Netmap.NodeInfo.Types.Attribute>();
            foreach (var item in tAttr)
                ass.Add(item.Value);
            n.Attributes.Clear();
            n.Attributes.AddRange(ass);
        }

        public Dictionary<string, API.Netmap.NodeInfo.Types.Attribute> UniqueAttributes(IEnumerator<API.Netmap.NodeInfo.Types.Attribute> attributes)
        {
            Dictionary<string, API.Netmap.NodeInfo.Types.Attribute> tAttr = new();
            while (attributes.MoveNext())
            {
                var attr = attributes.Current;
                tAttr[attr.Key] = attr;
            }
            return tAttr;
        }
    }
}

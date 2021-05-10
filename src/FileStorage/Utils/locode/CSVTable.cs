using Neo.FileStorage.Utils.locode.db;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils.locode
{
    public class CSVTable
    {
        private const int subDivCountry = 0;
        private const int subDivSubdivision = 1;
        private const int subDivName = 2;
        private const int subDivFldNum = 4;
        private Once subDivOnce;
        public string[] paths;
        public string subDivPath;
        public Dictionary<(string, string), string> mSubDiv;

        public void IterateAll(Action<LocodeRecord> f)
        {
            int wordsPerRecord = 12;
            ScanWords(paths, wordsPerRecord, (string[] words) =>
            {
                var lc = LOCODE.FromString(string.Join(" ", words.Skip(1).Take(2).ToArray()));
                var record = new LocodeRecord()
                {
                    Ch = words[0],
                    LOCODE = lc,
                    Name = words[3],
                    NameWoDiacritics = words[4],
                    SubDiv = words[5],
                    Function = words[6],
                    Status = words[7],
                    Date = words[8],
                    IATA = words[9],
                    Coordinates = words[10],
                    Remarks = words[11],
                };
                f(record);
            });
        }

        public string SubDivName(CountryCode countryCode, string code)
        {
            InitSubDiv();
            if (!mSubDiv.TryGetValue((countryCode.Symbols().ToString(), code), out string rec)) throw new Exception("subdivision not found");
            return rec;
        }

        public void InitSubDiv()
        {
            subDivOnce.Do(() =>
            {
                ScanWords(new string[] { subDivPath }, subDivFldNum, (string[] words) =>
                {
                    mSubDiv[(words[subDivCountry], words[subDivSubdivision])] = words[subDivName];
                });
            });
        }

        public void ScanWords(string[] paths, int fpr, Action<string[]> wordsHandler)
        {
            foreach (string path in paths)
            {
                string[] records = File.ReadAllLines(path);
                foreach (string record in records)
                {
                    var words = record.Split(',');
                    if (words.Length != fpr) throw new Exception("invalid table record");
                    wordsHandler(words);
                }
            }
        }
    }
}

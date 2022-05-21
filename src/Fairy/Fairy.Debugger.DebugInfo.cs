using Neo.IO.Json;
using Neo.VM;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        public struct SourceFilenameAndLineNum { public string SourceFilename; public uint LineNum; }
        readonly ConcurrentDictionary<UInt160, HashSet<SourceFilenameAndLineNum>> contractScriptHashToSourceLineNums = new();
        readonly ConcurrentDictionary<UInt160, Dictionary<uint, SourceFilenameAndLineNum>> contractScriptHashToInstructionPointerToSourceLineNum = new();
        readonly ConcurrentDictionary<UInt160, HashSet<string>> contractScriptHashToSourceLineFilenames = new();
        readonly ConcurrentDictionary<UInt160, Dictionary<uint, OpCode>> contractScriptHashToInstructionPointerToOpCode = new();
        readonly ConcurrentDictionary<UInt160, JObject> contractScriptHashToNefDbgNfo = new();

        private static readonly Regex opCodeRegex = new(@"^(\d+)\s(.*?)\s?(#\s.*)?$");  // 8039 SYSCALL 62-7D-5B-52 # System.Contract.Call SysCall
        private static readonly Regex sourceCodeRegex = new(@"^#\sCode\s(.*\.cs)\sline\s(\d+):\s""(.*)""$");  // # Code NFTLoan.cs line 523: "ExecutionEngine.Assert((bool)Contract.Call(token, "transfer", CallFlags.All, tenant, Runtime.ExecutingScriptHash, neededAmount, tokenId, TRANSACTION_DATA), "NFT payback failed");"
        private static readonly Regex methodStartRegex = new(@"^# Method\sStart\s(.*)$");  // # Method Start NFTLoan.NFTLoan.FlashBorrowDivisible
        private static readonly Regex methodEndRegex = new(@"^# Method\sEnd\s(.*)$");  // # Method End NFTLoan.NFTLoan.FlashBorrowDivisible

        public static string Unzip(byte[] zippedBuffer)
        {
            using var zippedStream = new MemoryStream(zippedBuffer);
            using var archive = new ZipArchive(zippedStream);
            var entry = archive.Entries.FirstOrDefault();
            if (entry != null)
            {
                using var unzippedEntryStream = entry.Open();
                using var ms = new MemoryStream();
                unzippedEntryStream.CopyTo(ms);
                var unzippedArray = ms.ToArray();
                return Encoding.Default.GetString(unzippedArray);
            }
            throw new ArgumentException("No file found in zip archive");
        }

        [RpcMethod]
        protected virtual JObject SetDebugInfo(JArray _params)
        {
            string param0 = _params[0].AsString();
            UInt160 scriptHash = UInt160.Parse(param0);
            // nccs YourContractProject.csproj --debug
            // find .nefdbgnfo beside your .nef contract, and
            // give me the base64encode(content) of .nefdbgnfo file
            JObject nefDbgNfo = JObject.Parse(Unzip(Convert.FromBase64String(_params[1].AsString())));
            contractScriptHashToNefDbgNfo[scriptHash] = nefDbgNfo;
            // https://github.com/devhawk/DumpNef
            // dumpnef contract.nef > contract.nef.txt
            // give me the content of that txt file!
            string dumpNef = _params[2].AsString();
            string[] lines = dumpNef.Replace("\r", "").Split("\n", StringSplitOptions.RemoveEmptyEntries);

            HashSet<SourceFilenameAndLineNum> sourceFilenameAndLineNums = new();
            contractScriptHashToSourceLineNums[scriptHash] = sourceFilenameAndLineNums;
            Dictionary<uint, SourceFilenameAndLineNum> InstructionPointerToSourceLineNum = new();
            contractScriptHashToInstructionPointerToSourceLineNum[scriptHash] = InstructionPointerToSourceLineNum;
            Dictionary<uint, OpCode> instructionPointerToOpCode = new();
            contractScriptHashToInstructionPointerToOpCode[scriptHash] = instructionPointerToOpCode;
            HashSet<string> filenames = new();
            contractScriptHashToSourceLineFilenames[scriptHash] = filenames;

            uint lineNum;
            for (lineNum = 0; lineNum < lines.Length; ++lineNum)
            {
                // foreach (var field in typeof(DumpNefPatterns).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                //     Console.WriteLine($"{field.Name}: {field.GetValue(dumpNefPatterns)}");
                Match match;
                match = sourceCodeRegex.Match(lines[lineNum]);
                if (match.Success)
                {
                    GroupCollection sourceCodeGroups = match.Groups;
                    uint sourceCodeLineNum = uint.Parse(sourceCodeGroups[2].ToString());
                    match = opCodeRegex.Match(lines[lineNum + 1]);
                    if (match.Success)
                    {
                        GroupCollection opcodeGroups = match.Groups;
                        uint instructionPointer = uint.Parse(opcodeGroups[1].ToString());
                        string filename = sourceCodeGroups[1].ToString();
                        filenames.Add(filename);
                        SourceFilenameAndLineNum sourceFilenameAndLineNum = new SourceFilenameAndLineNum { SourceFilename = filename, LineNum = sourceCodeLineNum };
                        InstructionPointerToSourceLineNum[instructionPointer] = sourceFilenameAndLineNum;
                        sourceFilenameAndLineNums.Add(sourceFilenameAndLineNum);
                    }
                    continue;
                }
                match = opCodeRegex.Match(lines[lineNum]);
                if (match.Success)
                {
                    GroupCollection opcodeGroups = match.Groups;
                    uint instructionPointer = uint.Parse(opcodeGroups[1].ToString());
                    string[] opcodeAndOperand = opcodeGroups[2].ToString().Split();
                    instructionPointerToOpCode[instructionPointer] = (OpCode)Enum.Parse(typeof(OpCode), opcodeAndOperand[0]);
                    continue;
                }
            }
            JObject json = new();
            json[param0] = true;
            return json;
        }

        [RpcMethod]
        protected virtual JObject ListDebugInfo(JArray _params)
        {
            JArray scriptHashes = new JArray();
            foreach (UInt160 s in contractScriptHashToInstructionPointerToSourceLineNum.Keys)
            {
                scriptHashes.Add(s.ToString());
            }
            return scriptHashes;
        }

        [RpcMethod]
        protected virtual JObject ListFilenamesOfContract(JArray _params)
        {
            string scriptHashStr = _params[0].AsString();
            UInt160 scriptHash = UInt160.Parse(scriptHashStr);
            List<string> filenameList = contractScriptHashToSourceLineFilenames[scriptHash].ToList();
            filenameList.Sort();
            JArray filenames = new JArray();
            foreach (string filename in filenameList)
            {
                filenames.Add(filename);
            }
            return filenames;
        }

        [RpcMethod]
        protected virtual JObject DeleteDebugInfo(JArray _params)
        {
            JObject json = new();
            foreach (var s in _params)
            {
                string str = s.AsString();
                UInt160 scriptHash = UInt160.Parse(str);
                contractScriptHashToInstructionPointerToSourceLineNum.Remove(scriptHash, out _);
                contractScriptHashToNefDbgNfo.Remove(scriptHash, out _);
                contractScriptHashToSourceLineFilenames.Remove(scriptHash, out _);
                contractScriptHashToAssemblyBreakpoints.Remove(scriptHash, out _);
                contractScriptHashToSourceCodeBreakpoints.Remove(scriptHash, out _);
                json[str] = true;
            }
            return json;
        }

        [RpcMethod]
        protected virtual JObject GetMethodByInstructionPointer(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            uint instrcutionPointer = uint.Parse(_params[1].AsString());
            foreach (JObject method in (JArray)contractScriptHashToNefDbgNfo[scriptHash]["methods"])
            {
                string[] rangeStr = method["range"].AsString().Split("-");
                if (instrcutionPointer >= uint.Parse(rangeStr[0]) && instrcutionPointer <= uint.Parse(rangeStr[1]))
                    return method;
            }
            return JObject.Null;
        }
    }
}

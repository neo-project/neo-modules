using Neo.IO.Json;
using System.Collections.Concurrent;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        readonly ConcurrentDictionary<UInt160, HashSet<uint>> contractScriptHashToAssemblyBreakpoints = new();
        readonly ConcurrentDictionary<UInt160, HashSet<SourceFilenameAndLineNum>> contractScriptHashToSourceCodeBreakpoints = new();

        [RpcMethod]
        protected virtual JObject SetAssemblyBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            if (!contractScriptHashToInstructionPointerToSourceLineNum.ContainsKey(scriptHash))
            {
                throw new ArgumentException("Scripthash not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            HashSet<uint>? assemblyBreakpoints;
            if (!contractScriptHashToAssemblyBreakpoints.TryGetValue(scriptHash, out assemblyBreakpoints))
            {
                assemblyBreakpoints = new HashSet<uint>();
                contractScriptHashToAssemblyBreakpoints[scriptHash] = assemblyBreakpoints;
            }
            JObject json = new();
            for (int i = 1; i < _params.Count; i++)
            {
                string breakpointInstructionPointerStr = _params[i].AsString();
                uint breakpointInstructionPointer = uint.Parse(breakpointInstructionPointerStr);
                if (contractScriptHashToInstructionPointerToOpCode[scriptHash].ContainsKey(breakpointInstructionPointer))
                    json[breakpointInstructionPointerStr] = assemblyBreakpoints.Add(breakpointInstructionPointer);
                else
                    throw new ArgumentException($"No instruction at InstructionPointer={breakpointInstructionPointer}");
            }
            return json;
        }

        [RpcMethod]
        protected virtual JObject ListAssemblyBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            if (!contractScriptHashToInstructionPointerToSourceLineNum.ContainsKey(scriptHash))
            {
                throw new ArgumentException("Scripthash not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            List<uint> assemblyBreakpoints = contractScriptHashToAssemblyBreakpoints[scriptHash].ToList();
            assemblyBreakpoints.Sort();
            JArray breakpointList = new();
            foreach (uint breakpointInstructionPointer in assemblyBreakpoints)
            {
                breakpointList.Add(breakpointInstructionPointer);
            }
            return breakpointList;
        }

        [RpcMethod]
        protected virtual JObject DeleteAssemblyBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            if (!contractScriptHashToInstructionPointerToSourceLineNum.ContainsKey(scriptHash))
            {
                throw new ArgumentException("Scripthash not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            JObject json = new();
            if (_params.Count == 1)  // delete all breakpoints
            {
                List<uint> assemblyBreakpoints = contractScriptHashToAssemblyBreakpoints[scriptHash].ToList();
                assemblyBreakpoints.Sort();
                foreach (uint breakpointInstructionPointer in assemblyBreakpoints)
                {
                    json[breakpointInstructionPointer.ToString()] = true;
                }
                contractScriptHashToAssemblyBreakpoints[scriptHash].Clear();
            }
            else
            {
                HashSet<uint> assemblyBreakpoints = contractScriptHashToAssemblyBreakpoints[scriptHash];
                for (int i = 1; i < _params.Count; i++)
                {
                    string breakpointInstructionPointerStr = _params[i].AsString();
                    uint breakpointInstructionPointer = uint.Parse(breakpointInstructionPointerStr);
                    json[breakpointInstructionPointerStr] = assemblyBreakpoints.Remove(breakpointInstructionPointer);
                }
            }
            return json;
        }

        [RpcMethod]
        protected virtual JObject SetSourceCodeBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            if (!contractScriptHashToInstructionPointerToSourceLineNum.ContainsKey(scriptHash))
            {
                throw new ArgumentException("Scripthash not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            HashSet<SourceFilenameAndLineNum>? sourceCodeBreakpoints;
            if (!contractScriptHashToSourceCodeBreakpoints.TryGetValue(scriptHash, out sourceCodeBreakpoints))
            {
                sourceCodeBreakpoints = new HashSet<SourceFilenameAndLineNum>();
                contractScriptHashToSourceCodeBreakpoints[scriptHash] = sourceCodeBreakpoints;
            }
            JArray breakpointList = new();
            int i = 1;
            while (_params.Count > i)
            {
                string sourceCodeFilename = _params[i].AsString();
                i++;
                uint sourceCodeBreakpointLineNum = uint.Parse(_params[i].AsString());
                i++;
                JObject json = new();
                SourceFilenameAndLineNum breakpoint = new SourceFilenameAndLineNum { SourceFilename = sourceCodeFilename, LineNum = sourceCodeBreakpointLineNum };
                if (contractScriptHashToSourceLineNums[scriptHash].Contains(breakpoint))
                {
                    sourceCodeBreakpoints.Add(breakpoint);
                    json["filename"] = sourceCodeFilename;
                    json["line"] = sourceCodeBreakpointLineNum;
                    breakpointList.Add(json);
                }
                else
                {
                    throw new ArgumentException($"No code at filename={sourceCodeFilename}, line={sourceCodeBreakpointLineNum}");
                }
            }
            return breakpointList;
        }

        [RpcMethod]
        protected virtual JObject ListSourceCodeBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            if (!contractScriptHashToInstructionPointerToSourceLineNum.ContainsKey(scriptHash))
            {
                throw new ArgumentException("Scripthash not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            List<SourceFilenameAndLineNum> sourceCodeBreakpoints = contractScriptHashToSourceCodeBreakpoints[scriptHash].OrderBy(p => p.SourceFilename).ThenBy(p => p.LineNum).ToList();
            JArray breakpointList = new();
            foreach (SourceFilenameAndLineNum sourceCodeBreakpointLineNum in sourceCodeBreakpoints)
            {
                JObject breakpoint = new JObject();
                breakpoint["filename"] = sourceCodeBreakpointLineNum.SourceFilename;
                breakpoint["line"] = sourceCodeBreakpointLineNum.LineNum;
                breakpointList.Add(breakpoint);
            }
            return breakpointList;
        }

        [RpcMethod]
        protected virtual JObject DeleteSourceCodeBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0].AsString());
            if (!contractScriptHashToInstructionPointerToSourceLineNum.ContainsKey(scriptHash))
            {
                throw new ArgumentException("Scripthash not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            JArray breakpointList = new();
            if (_params.Count == 1)  // delete all breakpoints
            {
                List<SourceFilenameAndLineNum> sourceCodeBreakpoints = contractScriptHashToSourceCodeBreakpoints[scriptHash].OrderBy(p => p.SourceFilename).ThenBy(p => p.LineNum).ToList();
                foreach (SourceFilenameAndLineNum sourceCodeBreakpointLineNum in sourceCodeBreakpoints)
                {
                    JObject json = new();
                    json["filename"] = sourceCodeBreakpointLineNum.SourceFilename;
                    json["line"] = sourceCodeBreakpointLineNum.LineNum;
                    breakpointList.Add(json);
                }
                contractScriptHashToSourceCodeBreakpoints[scriptHash].Clear();
            }
            else
            {
                HashSet<SourceFilenameAndLineNum> sourceCodeBreakpoints = contractScriptHashToSourceCodeBreakpoints[scriptHash];
                int i = 1;
                while (_params.Count > i)
                {
                    string sourceCodeBreakpointFilename = _params[i].AsString();
                    i++;
                    uint sourceCodeBreakpointLineNum = uint.Parse(_params[i].AsString());
                    i++;
                    if (sourceCodeBreakpoints.Remove(new SourceFilenameAndLineNum { SourceFilename = sourceCodeBreakpointFilename, LineNum = sourceCodeBreakpointLineNum }))
                    {
                        JObject json = new();
                        json["filename"] = sourceCodeBreakpointFilename;
                        json["line"] = sourceCodeBreakpointLineNum;
                        breakpointList.Add(json);
                    }
                }
            }
            return breakpointList;
        }
    }
}

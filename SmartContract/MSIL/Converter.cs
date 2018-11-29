﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Neo.Compiler.MSIL
{

    //public class Converter
    //{
    //    public static byte[] Convert(System.IO.Stream dllstream, ILogger logger = null)
    //    {
    //        var module = new ILModule();
    //        module.LoadModule(dllstream, null);
    //        if (logger == null)
    //        {
    //            logger = new DefLogger();
    //        }
    //        var converter = new ModuleConverter(logger);
    //        //有异常的话在 convert 函数中会直接throw 出来
    //        var antmodule = converter.Convert(module);
    //        return antmodule.Build();
    //    }

    //}
    class DefLogger : ILogger
    {
        public void Log(string log)
        {
            Console.WriteLine(log);
        }
    }
    /// <summary>
    /// 从ILCode 向小蚁 VM 转换的转换器
    /// </summary>
    public partial class ModuleConverter
    {
        public ModuleConverter(ILogger logger)
        {
            if (logger == null)
            {
                logger = new DefLogger();
            }
            this.logger = logger;
#if NET47
            try
            {
                var assm = System.Reflection.Assembly.GetAssembly(typeof(System.Action));
                var name = System.IO.Path.GetFileName(assm.Location);
                if (name.ToLower() == "mscorlib.dll")
                {
                    var path = System.IO.Path.GetFullPath(".");
                    System.IO.File.Copy(assm.Location, System.IO.Path.Combine(path, name));
                }
            }
            catch
            {

            }
            //assm.Location
#endif
        }

        ILogger logger;
        public NeoModule outModule;
        ILModule inModule;
        public Dictionary<ILMethod, NeoMethod> methodLink = new Dictionary<ILMethod, NeoMethod>();
        public NeoModule Convert(ILModule _in, ConvOption option = null)
        {
            this.inModule = _in;
            //logger.Log("beginConvert.");
            this.outModule = new NeoModule(this.logger);
            this.outModule.option = option == null ? ConvOption.Default : option;
            foreach (var t in _in.mapType)
            {
                if (t.Key.Contains("<"))
                    continue;//系统的，不要
                if (t.Key.Contains("_API_")) continue;//api的，不要
                if (t.Key.Contains(".My."))
                    continue;//vb system
                foreach (var m in t.Value.methods)
                {
                    if (m.Value.method == null) continue;
                    if (m.Value.method.IsAddOn || m.Value.method.IsRemoveOn)
                        continue;//event 自动生成的代码，不要
                    NeoMethod nm = new NeoMethod();
                    if (m.Key.Contains(".cctor"))
                    {
                        CctorSubVM.Parse(m.Value, this.outModule);
                        continue;
                    }
                    if (m.Value.method.IsConstructor) continue;
                    nm._namespace = m.Value.method.DeclaringType.FullName;
                    nm.name = m.Value.method.FullName;
                    nm.displayName = m.Value.method.Name;

                    Mono.Collections.Generic.Collection<Mono.Cecil.CustomAttribute> ca = m.Value.method.CustomAttributes;
                    foreach (var attr in ca)
                    {
                        if (attr.AttributeType.Name == "DisplayNameAttribute")
                        {
                            nm.displayName = (string)attr.ConstructorArguments[0].Value;
                        }
                    }
                    nm.inSmartContract = m.Value.method.DeclaringType.BaseType.Name == "SmartContract";
                    nm.isPublic = m.Value.method.IsPublic;
                    this.methodLink[m.Value] = nm;
                    outModule.mapMethods[nm.name] = nm;

                }
                foreach (var e in t.Value.fields)
                {
                    if (e.Value.isEvent)
                    {
                        NeoEvent ae = new NeoEvent();
                        ae._namespace = e.Value.field.DeclaringType.FullName;
                        ae.name = ae._namespace + "::" + e.Key;
                        ae.displayName = e.Value.displayName;
                        ae.returntype = e.Value.returntype;
                        ae.paramtypes = e.Value.paramtypes;
                        outModule.mapEvents[ae.name] = ae;
                    }
                }
            }

            var keys = new List<string>(_in.mapType.Keys);
            foreach (var key in keys)
            {
                var value = _in.mapType[key];
                if (key.Contains("<"))
                    continue;//系统的，不要
                if (key.Contains("_API_")) continue;//api的，不要
                if (key.Contains(".My."))
                    continue;//vb system

                foreach (var m in value.methods)
                {

                    if (m.Value.method == null) continue;
                    if (m.Key.Contains(".cctor"))
                    {
                        continue;
                    }
                    if (m.Value.method.IsAddOn || m.Value.method.IsRemoveOn)
                        continue;//event 自动生成的代码，不要

                    var nm = this.methodLink[m.Value];


                    //try
                    {
                        nm.returntype = m.Value.returntype;
                        try
                        {
                            var type = m.Value.method.ReturnType.Resolve();
                            foreach (var i in type.Interfaces)
                            {
                                if (i.InterfaceType.Name == "IApiInterface")
                                {
                                    nm.returntype = "IInteropInterface";
                                }
                            }
                        }
                        catch (Exception err)
                        {

                        }

                        foreach (var src in m.Value.paramtypes)
                        {
                            nm.paramtypes.Add(new NeoParam(src.name, src.type));
                        }

                        byte[] outcall; string name;
                        if (IsAppCall(m.Value.method, out outcall))
                            continue;
                        if (IsNonCall(m.Value.method))
                            continue;
                        if (IsOpCall(m.Value.method, out name))
                            continue;
                        if (IsSysCall(m.Value.method, out name))
                            continue;

                        this.ConvertMethod(m.Value, nm);
                    }
                    //catch (Exception err)
                    //{
                    //    logger.Log("error:" + err.Message);
                    //}
                }
            }
            //转换完了，做个link，全部拼到一起
            string mainmethod = "";

            foreach (var key in outModule.mapMethods.Keys)
            {

                if (key.Contains("::Main("))
                {
                    NeoMethod m = outModule.mapMethods[key];
                    if (m.inSmartContract)
                    {
                        foreach (var l in this.methodLink)
                        {
                            if (l.Value == m)
                            {
                                if (mainmethod != "")
                                    throw new Exception("Have too mush EntryPoint,Check it.");
                                mainmethod = key;
                            }
                        }
                    }

                }
            }
            if (mainmethod == "")
            {
                throw new Exception("Can't find EntryPoint,Check it.");
            }
            else
            {
                //单一默认入口
                logger.Log("Find entrypoint:" + mainmethod);
            }

            outModule.mainMethod = mainmethod;
            this.LinkCode(mainmethod);
            //this.findFirstFunc();//得找到第一个函数
            //然后给每个method 分配一个func addr
            //还需要对所有的call 做一次地址转换

            //this.outModule.Build();
            return outModule;
        }
        private void LinkCode(string main)
        {
            if (this.outModule.mapMethods.ContainsKey(main) == false)
            {
                throw new Exception("Can't find entrypoint:" + main);
            }
            var first = this.outModule.mapMethods[main];
            first.funcaddr = 0;
            this.outModule.total_Codes.Clear();
            int addr = 0;
            foreach (var c in first.body_Codes)
            {
                if (addr != c.Key)
                {
                    throw new Exception("sth error");
                }
                this.outModule.total_Codes[addr] = c.Value;
                addr += 1;
                if (c.Value.bytes != null)
                    addr += c.Value.bytes.Length;
            }

            foreach (var m in this.outModule.mapMethods)
            {
                if (m.Key == main) continue;

                m.Value.funcaddr = addr;

                foreach (var c in m.Value.body_Codes)
                {
                    this.outModule.total_Codes[addr] = c.Value;
                    addr += 1;
                    if (c.Value.bytes != null)
                        addr += c.Value.bytes.Length;

                    //地址偏移
                    c.Value.addr += m.Value.funcaddr;
                }
            }

            foreach (var c in this.outModule.total_Codes.Values)
            {
                if (c.needfixfunc)
                {//需要地址转换
                    var addrfunc = this.outModule.mapMethods[c.srcfunc].funcaddr;

                    if (c.bytes.Length > 2)
                    {
                        var len = c.bytes.Length - 2;
                        int wantaddr = addrfunc - c.addr - len;

                        if (wantaddr < Int16.MinValue || wantaddr > Int16.MaxValue)
                        {
                            throw new Exception("addr jump is too far.");
                        }
                        Int16 addrconv = (Int16)wantaddr;

                        var bts = BitConverter.GetBytes(addrconv);
                        c.bytes[c.bytes.Length - 2] = bts[0];
                        c.bytes[c.bytes.Length - 1] = bts[1];
                    }
                    else if (c.bytes.Length == 2)
                    {
                        int wantaddr = addrfunc - c.addr;

                        if (wantaddr < Int16.MinValue || wantaddr > Int16.MaxValue)
                        {
                            throw new Exception("addr jump is too far.");
                        }
                        Int16 addrconv = (Int16)wantaddr;
                        c.bytes = BitConverter.GetBytes(addrconv);
                    }
                    else
                    {
                        throw new Exception("not have right fill bytes");
                    }
                    c.needfixfunc = false;
                }
            }
        }

        private void ConvertMethod(ILMethod from, NeoMethod to)
        {


            this.addr = 0;
            this.addrconv.Clear();

            //插入一个记录深度的代码，再往前的是参数
            _insertBeginCode(from, to);

            int skipcount = 0;
            foreach (var src in from.body_Codes.Values)
            {
                if (skipcount > 0)
                {
                    skipcount--;
                }
                else
                {
                    //在return之前加入清理参数代码
                    if (src.code == CodeEx.Ret)//before return 
                    {
                        _insertEndCode(from, to, src);
                    }
                    try
                    {
                        skipcount = ConvertCode(from, src, to);
                    }
                    catch (Exception err)
                    {
                        throw new Exception("error:" + from.method.FullName + "::" + src, err);
                    }
                }
            }

            ConvertAddrInMethod(to);
        }

        Dictionary<int, int> addrconv = new Dictionary<int, int>();
        int addr = 0;

        //Dictionary<string, string[]> srccodes = new Dictionary<string, string[]>();
        //string getSrcCode(string url, int line)
        //{
        //    if (url == null || url == "") return "";
        //    if (srccodes.ContainsKey(url) == false)
        //    {
        //        srccodes[url] = System.IO.File.ReadAllLines(url);
        //    }
        //    if (srccodes.ContainsKey(url) != false)
        //    {
        //        var file = srccodes[url];
        //        if (line > 0 && line <= file.Length)
        //        {
        //            return file[line - 1];
        //        }
        //    }
        //    return "";
        //}
        static int getNumber(NeoCode code)
        {
            if (code.code <= VM.OpCode.PUSHBYTES75 && code.code >= VM.OpCode.PUSHBYTES1)
                return (int)new BigInteger(code.bytes);
            else if (code.code == VM.OpCode.PUSH0) return 0;
            else if (code.code == VM.OpCode.PUSH1) return 1;
            else if (code.code == VM.OpCode.PUSH2) return 2;
            else if (code.code == VM.OpCode.PUSH3) return 3;
            else if (code.code == VM.OpCode.PUSH4) return 4;
            else if (code.code == VM.OpCode.PUSH5) return 5;
            else if (code.code == VM.OpCode.PUSH6) return 6;
            else if (code.code == VM.OpCode.PUSH7) return 7;
            else if (code.code == VM.OpCode.PUSH8) return 8;
            else if (code.code == VM.OpCode.PUSH9) return 9;
            else if (code.code == VM.OpCode.PUSH10) return 10;
            else if (code.code == VM.OpCode.PUSH11) return 11;
            else if (code.code == VM.OpCode.PUSH12) return 12;
            else if (code.code == VM.OpCode.PUSH13) return 13;
            else if (code.code == VM.OpCode.PUSH14) return 14;
            else if (code.code == VM.OpCode.PUSH15) return 15;
            else if (code.code == VM.OpCode.PUSH16) return 16;
            else if (code.code == VM.OpCode.PUSHDATA1) return pushdata1bytes2int(code.bytes);
            else
                throw new Exception("not support getNumber From this:" + code.ToString());
        }
        static int pushdata1bytes2int(byte[] data)
        {
            byte[] target = new byte[4];
            for (var i = 1; i < data.Length; i++)
                target[i - 1] = data[i];
            var n = BitConverter.ToInt32(target, 0);
            return n;
        }
        private void ConvertAddrInMethod(NeoMethod to)
        {
            foreach (var c in to.body_Codes.Values)
            {
                if (c.needfix)
                {

                    try
                    {
                        var _addr = addrconv[c.srcaddr];
                        Int16 addroff = (Int16)(_addr - c.addr);
                        c.bytes = BitConverter.GetBytes(addroff);
                        c.needfix = false;
                    }
                    catch
                    {
                        throw new Exception("cannot convert addr in: " + to.name + "\r\n");
                    }



                }
            }
        }
        private int ConvertCode(ILMethod method, OpCode src, NeoMethod to)
        {
            int skipcount = 0;
            switch (src.code)
            {
                case CodeEx.Nop:
                    _Convert1by1(VM.OpCode.NOP, src, to);
                    break;
                case CodeEx.Ret:
                    //return 在外面特殊处理了
                    _Insert1(VM.OpCode.RET, null, to);
                    break;
                case CodeEx.Pop:
                    _Convert1by1(VM.OpCode.DROP, src, to);
                    break;

                case CodeEx.Ldnull:
                    _ConvertPush(new byte[0], src, to);
                    break;

                case CodeEx.Ldc_I4:
                case CodeEx.Ldc_I4_S:
                    skipcount = _ConvertPushI4WithConv(method, src.tokenI32, src, to);
                    break;
                case CodeEx.Ldc_I4_0:
                    _ConvertPush(0, src, to);
                    break;
                case CodeEx.Ldc_I4_1:
                    _ConvertPush(1, src, to);
                    break;
                case CodeEx.Ldc_I4_2:
                    _ConvertPush(2, src, to);
                    break;
                case CodeEx.Ldc_I4_3:
                    _ConvertPush(3, src, to);
                    break;
                case CodeEx.Ldc_I4_4:
                    _ConvertPush(4, src, to);
                    break;
                case CodeEx.Ldc_I4_5:
                    _ConvertPush(5, src, to);
                    break;
                case CodeEx.Ldc_I4_6:
                    _ConvertPush(6, src, to);
                    break;
                case CodeEx.Ldc_I4_7:
                    _ConvertPush(7, src, to);
                    break;
                case CodeEx.Ldc_I4_8:
                    _ConvertPush(8, src, to);
                    break;
                case CodeEx.Ldc_I4_M1:
                    skipcount = _ConvertPushI4WithConv(method, -1, src, to);
                    break;
                case CodeEx.Ldc_I8:
                    skipcount = _ConvertPushI8WithConv(method, src.tokenI64, src, to);
                    break;
                case CodeEx.Ldstr:
                    _ConvertPush(Encoding.UTF8.GetBytes(src.tokenStr), src, to);
                    break;
                case CodeEx.Stloc_0:
                    _ConvertStLoc(method, src, to, 0);
                    break;
                case CodeEx.Stloc_1:
                    _ConvertStLoc(method, src, to, 1);
                    break;
                case CodeEx.Stloc_2:
                    _ConvertStLoc(method, src, to, 2);
                    break;
                case CodeEx.Stloc_3:
                    _ConvertStLoc(method, src, to, 3);
                    break;
                case CodeEx.Stloc_S:
                    _ConvertStLoc(method, src, to, src.tokenI32);
                    break;

                case CodeEx.Ldloc_0:
                    _ConvertLdLoc(method, src, to, 0);
                    break;
                case CodeEx.Ldloc_1:
                    _ConvertLdLoc(method, src, to, 1);
                    break;
                case CodeEx.Ldloc_2:
                    _ConvertLdLoc(method, src, to, 2);
                    break;
                case CodeEx.Ldloc_3:
                    _ConvertLdLoc(method, src, to, 3);
                    break;
                case CodeEx.Ldloc_S:
                    _ConvertLdLoc(method, src, to, src.tokenI32);
                    break;

                case CodeEx.Ldarg_0:
                    _ConvertLdArg(method, src, to, 0);
                    break;
                case CodeEx.Ldarg_1:
                    _ConvertLdArg(method, src, to, 1);
                    break;
                case CodeEx.Ldarg_2:
                    _ConvertLdArg(method, src, to, 2);
                    break;
                case CodeEx.Ldarg_3:
                    _ConvertLdArg(method, src, to, 3);
                    break;
                case CodeEx.Ldarg_S:
                case CodeEx.Ldarg:
                case CodeEx.Ldarga:
                case CodeEx.Ldarga_S:
                    _ConvertLdArg(method, src, to, src.tokenI32);
                    break;

                case CodeEx.Starg_S:
                case CodeEx.Starg:
                    _ConvertStArg(src, to, src.tokenI32);
                    break;
                //需要地址轉換的情況
                case CodeEx.Br:
                case CodeEx.Br_S:
                case CodeEx.Leave:
                case CodeEx.Leave_S:
                    {
                        var code = _Convert1by1(VM.OpCode.JMP, src, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }

                    break;
                case CodeEx.Switch:
                    {
                        throw new Exception("need neo.VM update.");
                        //var addrdata = new byte[src.tokenAddr_Switch.Length * 2 + 2];
                        //var shortaddrcount = (UInt16)src.tokenAddr_Switch.Length;
                        //var data = BitConverter.GetBytes(shortaddrcount);
                        //addrdata[0] = data[0];
                        //addrdata[1] = data[1];
                        //var code = _Convert1by1(VM.OpCode.SWITCH, src, to, addrdata);
                        //code.needfix = true;
                        //code.srcaddrswitch = new int[shortaddrcount];
                        //for (var i = 0; i < shortaddrcount; i++)
                        //{
                        //    code.srcaddrswitch[i] = src.tokenAddr_Switch[i];
                        //}
                    }
                    break;
                case CodeEx.Brtrue:
                case CodeEx.Brtrue_S:
                    {
                        var code = _Convert1by1(VM.OpCode.JMPIF, src, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Brfalse:
                case CodeEx.Brfalse_S:
                    {
                        var code = _Convert1by1(VM.OpCode.JMPIFNOT, src, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Beq:
                case CodeEx.Beq_S:
                    {
                        _Convert1by1(VM.OpCode.NUMEQUAL, src, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Bne_Un:
                case CodeEx.Bne_Un_S:
                    {
                        _Convert1by1(VM.OpCode.ABS, src, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.ABS, null, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.NUMNOTEQUAL, null, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Blt:
                case CodeEx.Blt_S:
                    {
                        _Convert1by1(VM.OpCode.LT, src, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Blt_Un:
                case CodeEx.Blt_Un_S:
                    {
                        _Convert1by1(VM.OpCode.ABS, src, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.ABS, null, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.LT, null, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Ble:
                case CodeEx.Ble_S:
                    {
                        _Convert1by1(VM.OpCode.LTE, src, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Ble_Un:
                case CodeEx.Ble_Un_S:
                    {
                        _Convert1by1(VM.OpCode.ABS, src, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.ABS, null, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.LTE, null, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Bgt:
                case CodeEx.Bgt_S:
                    {
                        _Convert1by1(VM.OpCode.GT, src, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Bgt_Un:
                case CodeEx.Bgt_Un_S:
                    {
                        _Convert1by1(VM.OpCode.ABS, src, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.ABS, null, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.GT, null, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Bge:
                case CodeEx.Bge_S:
                    {

                        _Convert1by1(VM.OpCode.GTE, src, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;
                case CodeEx.Bge_Un:
                case CodeEx.Bge_Un_S:
                    {
                        _Convert1by1(VM.OpCode.ABS, src, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.ABS, null, to);
                        _Convert1by1(VM.OpCode.SWAP, null, to);
                        _Convert1by1(VM.OpCode.GTE, null, to);
                        var code = _Convert1by1(VM.OpCode.JMPIF, null, to, new byte[] { 0, 0 });
                        code.needfix = true;
                        code.srcaddr = src.tokenAddr_Index;
                    }
                    break;

                //Stack
                case CodeEx.Dup:
                    _Convert1by1(VM.OpCode.DUP, src, to);
                    break;

                //Bitwise logic
                case CodeEx.And:
                    _Convert1by1(VM.OpCode.AND, src, to);
                    break;
                case CodeEx.Or:
                    _Convert1by1(VM.OpCode.OR, src, to);
                    break;
                case CodeEx.Xor:
                    _Convert1by1(VM.OpCode.XOR, src, to);
                    break;
                case CodeEx.Not:
                    _Convert1by1(VM.OpCode.INVERT, src, to);
                    break;

                //math
                case CodeEx.Add:
                case CodeEx.Add_Ovf:
                case CodeEx.Add_Ovf_Un:
                    _Convert1by1(VM.OpCode.ADD, src, to);
                    break;
                case CodeEx.Sub:
                case CodeEx.Sub_Ovf:
                case CodeEx.Sub_Ovf_Un:
                    _Convert1by1(VM.OpCode.SUB, src, to);
                    break;
                case CodeEx.Mul:
                case CodeEx.Mul_Ovf:
                case CodeEx.Mul_Ovf_Un:
                    _Convert1by1(VM.OpCode.MUL, src, to);
                    break;
                case CodeEx.Div:
                case CodeEx.Div_Un:
                    _Convert1by1(VM.OpCode.DIV, src, to);
                    break;
                case CodeEx.Rem:
                case CodeEx.Rem_Un:
                    _Convert1by1(VM.OpCode.MOD, src, to);
                    break;
                case CodeEx.Neg:
                    _Convert1by1(VM.OpCode.NEGATE, src, to);
                    break;
                case CodeEx.Shl:
                    _Convert1by1(VM.OpCode.SHL, src, to);
                    break;
                case CodeEx.Shr:
                case CodeEx.Shr_Un:
                    _Convert1by1(VM.OpCode.SHR, src, to);
                    break;

                //logic
                case CodeEx.Clt:
                case CodeEx.Clt_Un:
                    _Convert1by1(VM.OpCode.LT, src, to);
                    break;
                case CodeEx.Cgt:
                case CodeEx.Cgt_Un:
                    _Convert1by1(VM.OpCode.GT, src, to);
                    break;
                case CodeEx.Ceq:
                    _Convert1by1(VM.OpCode.NUMEQUAL, src, to);
                    break;

                //call
                case CodeEx.Call:
                case CodeEx.Callvirt:
                    _ConvertCall(src, to);
                    break;

                //用上一个参数作为数量，new 一个数组
                case CodeEx.Newarr:
                    skipcount = _ConvertNewArr(method, src, to);
                    break;


                //array
                case CodeEx.Ldelem_U1://用意为byte[] 取一部分.....
                    _ConvertPush(1, src, to);
                    _Convert1by1(VM.OpCode.SUBSTR, null, to);
                    break;
                case CodeEx.Ldelem_Any:
                case CodeEx.Ldelem_I:
                case CodeEx.Ldelem_I1:
                case CodeEx.Ldelem_I2:
                case CodeEx.Ldelem_I4:
                case CodeEx.Ldelem_I8:
                case CodeEx.Ldelem_R4:
                case CodeEx.Ldelem_R8:
                case CodeEx.Ldelem_Ref:
                case CodeEx.Ldelem_U2:
                case CodeEx.Ldelem_U4:
                    _Convert1by1(VM.OpCode.PICKITEM, src, to);
                    break;
                case CodeEx.Ldlen:
                    _Convert1by1(VM.OpCode.ARRAYSIZE, src, to);
                    break;
                case CodeEx.Stelem_Any:
                case CodeEx.Stelem_I:
                case CodeEx.Stelem_I1:
                case CodeEx.Stelem_I2:
                case CodeEx.Stelem_I4:
                case CodeEx.Stelem_I8:
                case CodeEx.Stelem_R4:
                case CodeEx.Stelem_R8:
                case CodeEx.Stelem_Ref:
                    _Convert1by1(VM.OpCode.SETITEM, src, to);
                    break;

                case CodeEx.Isinst://支持处理as 表达式
                    break;
                case CodeEx.Castclass:
                    _ConvertCastclass(method, src, to);
                    break;

                case CodeEx.Box:
                case CodeEx.Unbox:
                case CodeEx.Unbox_Any:
                case CodeEx.Break:
                //也有可能以后利用这个断点调试
                case CodeEx.Conv_I:
                case CodeEx.Conv_I1:
                case CodeEx.Conv_I2:
                case CodeEx.Conv_I4:
                case CodeEx.Conv_I8:
                case CodeEx.Conv_Ovf_I:
                case CodeEx.Conv_Ovf_I_Un:
                case CodeEx.Conv_Ovf_I1:
                case CodeEx.Conv_Ovf_I1_Un:
                case CodeEx.Conv_Ovf_I2:
                case CodeEx.Conv_Ovf_I2_Un:
                case CodeEx.Conv_Ovf_I4:
                case CodeEx.Conv_Ovf_I4_Un:
                case CodeEx.Conv_Ovf_I8:
                case CodeEx.Conv_Ovf_I8_Un:
                case CodeEx.Conv_Ovf_U:
                case CodeEx.Conv_Ovf_U_Un:
                case CodeEx.Conv_Ovf_U1:
                case CodeEx.Conv_Ovf_U1_Un:
                case CodeEx.Conv_Ovf_U2:
                case CodeEx.Conv_Ovf_U2_Un:
                case CodeEx.Conv_Ovf_U4:
                case CodeEx.Conv_Ovf_U4_Un:
                case CodeEx.Conv_Ovf_U8:
                case CodeEx.Conv_Ovf_U8_Un:
                case CodeEx.Conv_U:
                case CodeEx.Conv_U1:
                case CodeEx.Conv_U2:
                case CodeEx.Conv_U4:
                case CodeEx.Conv_U8:
                    break;

                ///////////////////////////////////////////////
                //以下因为支持结构体而出现
                //加载一个引用，这里改为加载一个pos值
                case CodeEx.Ldloca:
                case CodeEx.Ldloca_S:
                    _ConvertLdLocA(method, src, to, src.tokenI32);
                    break;
                case CodeEx.Initobj:
                    _ConvertInitObj(src, to);
                    break;
                case CodeEx.Newobj:
                    _ConvertNewObj(src, to);
                    break;
                case CodeEx.Stfld:
                    _ConvertStfld(method, src, to);
                    break;
                case CodeEx.Ldfld:
                    _ConvertLdfld(src, to);
                    break;

                case CodeEx.Ldsfld:

                    {
                        _Convert1by1(VM.OpCode.NOP, src, to);
                        var d = src.tokenUnknown as Mono.Cecil.FieldDefinition;
                        //如果是readonly，可以pull个常量上来的
                        if (
                            ((d.Attributes & Mono.Cecil.FieldAttributes.InitOnly) > 0) &&
                            ((d.Attributes & Mono.Cecil.FieldAttributes.Static) > 0)
                            )
                        {
                            var fname = d.DeclaringType.FullName + "::" + d.Name;
                            var _src = outModule.staticfields[fname];
                            if (_src is byte[])
                            {
                                var bytesrc = (byte[])_src;
                                _ConvertPush(bytesrc, src, to);
                            }
                            else if (_src is int)
                            {
                                var intsrc = (int)_src;
                                _ConvertPush(intsrc, src, to);
                            }
                            else if (_src is long)
                            {
                                var intsrc = (long)_src;
                                _ConvertPush(intsrc, src, to);

                            }
                            else if (_src is Boolean)
                            {
                                var bsrc = (Boolean)_src;
                                _ConvertPush(bsrc ? 1 : 0, src, to);
                            }
                            else if (_src is string)
                            {
                                var bytesrc = System.Text.Encoding.UTF8.GetBytes((string)_src);
                                _ConvertPush(bytesrc, src, to);
                            }
                            else if (_src is BigInteger)
                            {
                                byte[] bytes = ((BigInteger)_src).ToByteArray();
                                _ConvertPush(bytes, src, to);

                            }
                            else
                            {
                                throw new Exception("not support type Ldsfld\r\n   in: " + to.name + "\r\n");
                            }
                            break;
                        }


                        //如果是调用event导致的这个代码，只找出他的名字
                        if (d.DeclaringType.HasEvents)
                        {
                            foreach (var ev in d.DeclaringType.Events)
                            {
                                if (ev.Name == d.Name && ev.EventType.FullName == d.FieldType.FullName)
                                {

                                    Mono.Collections.Generic.Collection<Mono.Cecil.CustomAttribute> ca = ev.CustomAttributes;
                                    to.lastsfieldname = d.Name;
                                    foreach (var attr in ca)
                                    {
                                        if (attr.AttributeType.Name == "DisplayNameAttribute")
                                        {
                                            to.lastsfieldname = (string)attr.ConstructorArguments[0].Value;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        else
                        {//如果走到这里，是一个静态成员，但是没有添加readonly 表示
                            throw new Exception("Just allow defined a static variable with readonly."+d.FullName);
                        }
                    }
                    break;
                case CodeEx.Throw:
                    {
                        _Convert1by1(VM.OpCode.THROW, src, to);//throw 会让vm 挂起
                        //不需要再插入return
                        //_Insert1(VM.OpCode.RET, "", to);
                    }
                    break;
                default:
#if WITHPDB
                    logger.Log("unsupported instruction " + src.code + "\r\n   in: " + to.name + "\r\n");
                    break;
#else
                    throw new Exception("unsupported instruction " + src.code + "\r\n   in: " + to.name + "\r\n");
#endif
            }

            return skipcount;
        }

    }
}

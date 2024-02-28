using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MoonSharp.Interpreter;
using _KopiLua = KopiLua.Lua;

namespace YANCL.Benchmark
{
    [MemoryDiagnoser]
    [WarmupCount(3)]
    [IterationCount(3)]
    // [EventPipeProfiler(EventPipeProfile.CpuSampling)]
    // [DotTraceDiagnoser]
    // [SimpleJob(RuntimeMoniker.NativeAot80)]
    [InProcess]
    public class Benchmarks
    {
        public class BenchmarkData
        {
            public string Name;
            public string Code;
            public int Type;

            public override string ToString() => Name;
        }

        public IEnumerable<BenchmarkData> GetCode()
        {
            yield return new BenchmarkData {
                Name = "Hot loop",
                Code = File.ReadAllText("./lua/loopAdd.lua")
            };
            yield return new BenchmarkData {
                Name = "Large file",
                Code = File.ReadAllText("./lua/loadBigFile.lua")
            };
            yield return new BenchmarkData {
                Name = "CLR interop",
                Type = 1,
                Code = File.ReadAllText("./lua/call.lua")
            };
            yield return new BenchmarkData {
                Name = "Delegate interop",
                Type = 2,
                Code = File.ReadAllText("./lua/call.lua")
            };
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void YANCL(BenchmarkData Benchmark) {
            var c = new Lua();
            StdLib.Basic.Load(c.Globals);
            switch (Benchmark.Type) {
                case 1:
                    c.Globals["globalFn"] = (LuaCFunction)((LuaThread s) => {
                        return s.Return(s.Number(1) + s.Number(2));
                    });
                    break;
                case 2:
                    c.Globals["globalFn"] = LuaValue.From<Func<int, int, int>>((a, b) => a + b);
                    break;
            }
            c.DoString(Benchmark.Code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void MoonSharp(BenchmarkData Benchmark) {
            var c = new Script();
            switch (Benchmark.Type) {
                case 1:
                    c.Globals["globalFn"] = new CallbackFunction((s, args) => {
                        return DynValue.NewNumber(args[0].Number + args[1].Number);
                    });
                    break;
                case 2:
                    c.Globals["globalFn"] = (Func<int, int, int>)((a, b) => a + b);
                    break;
            }
            c.DoString(Benchmark.Code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void KeraLua(BenchmarkData Benchmark) {
            var c = new KeraLua.Lua();
            switch (Benchmark.Type) {
                case 1:
                    c.PushCFunction((L) => {
                        c.PushNumber(c.ToNumber(1) + c.ToNumber(2));
                        return 1;
                    });
                    c.SetGlobal("globalFn");
                    break;
                case 2:
                    throw new NotImplementedException();
            }
            c.DoString(Benchmark.Code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void KopiLua(BenchmarkData Benchmark) {
            var L = _KopiLua.lua_open();
            _KopiLua.luaL_openlibs(L);
            switch (Benchmark.Type) {
                case 1:
                    _KopiLua.lua_pushcclosure(L, (L) => {
                        _KopiLua.lua_pushnumber(L, _KopiLua.lua_tonumber(L, 1) + _KopiLua.lua_tonumber(L, 2));
                        return 1;
                    }, 0);
                    _KopiLua.lua_setglobal(L, "globalFn");
                    break;
                case 2:
                    throw new NotImplementedException();
            }
            _KopiLua.luaL_loadbuffer(L, Benchmark.Code, (uint)Benchmark.Code.Length, "file");
            _KopiLua.lua_pcall(L, 0, 0, 0);
        }
    }
}
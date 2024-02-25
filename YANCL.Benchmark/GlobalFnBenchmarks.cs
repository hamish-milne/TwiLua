using System.Collections;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Running;
using MoonSharp.Interpreter;

namespace YANCL.Benchmark
{
    [MemoryDiagnoser]
    [WarmupCount(3)]
    [IterationCount(10)]
    // [EventPipeProfiler(EventPipeProfile.CpuSampling)]
    // [DotTraceDiagnoser]
    [InProcess]
    public class GlobalFnBenchmarks
    {
        public IEnumerable<string> GetCode()
        {
            yield return File.ReadAllText("./lua/call.lua");
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void YANCL(string code) {
            var c = new Lua();
            c.Globals["globalFn"] = (LuaCFunction)((LuaThread s) => {
                return s.Return(s.Number(1) + s.Number(2));
            });
            c.DoString(code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void MoonSharp(string code) {
            var c = new Script();
            c.Globals["globalFn"] = new CallbackFunction((s, args) => {
                return DynValue.NewNumber(args[0].Number + args[1].Number);
            });
            c.DoString(code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void KeraLua(string code) {
            var c = new KeraLua.Lua();
            c.PushCFunction((L) => {
                c.PushNumber(c.ToNumber(1) + c.ToNumber(2));
                return 1;
            });
            c.SetGlobal("globalFn");
            c.DoString(code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void _KopiLua(string code) {
            var L = KopiLua.Lua.lua_open();
            KopiLua.Lua.luaL_openlibs(L);
            KopiLua.Lua.lua_pushcclosure(L, (L) => {
                KopiLua.Lua.lua_pushnumber(L, KopiLua.Lua.lua_tonumber(L, 1) + KopiLua.Lua.lua_tonumber(L, 2));
                return 1;
            }, 0);
            KopiLua.Lua.lua_setglobal(L, "globalFn");
            KopiLua.Lua.luaL_loadbuffer(L, code, (uint)code.Length, "loopAdd.lua");
            KopiLua.Lua.lua_pcall(L, 0, 0, 0);
        }
        
    }
}
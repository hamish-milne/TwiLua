using System.Collections;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace YANCL.Benchmark
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        public IEnumerable<string> GetCode()
        {
            yield return File.ReadAllText("./lua/loopAdd.lua");
            yield return File.ReadAllText("./lua/loadBigFile.lua");
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void YANCL(string code) {
            var c = new Lua();
            c.DoString(code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void MoonSharp(string code) {
            var c = new MoonSharp.Interpreter.Script();
            c.DoString(code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void KeraLua(string code) {
            var c = new KeraLua.Lua();
            c.DoString(code);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetCode))]
        public void _KopiLua(string code) {
            var L = KopiLua.Lua.lua_open();
            KopiLua.Lua.luaL_openlibs(L);
            KopiLua.Lua.luaL_loadbuffer(L, code, (uint)code.Length, "loopAdd.lua");
            KopiLua.Lua.lua_pcall(L, 0, 0, 0);
        }
        
    }
}
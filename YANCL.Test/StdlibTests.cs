using System;
using System.IO;
using Xunit;

namespace YANCL.Test
{
    public class StdlibTests
    {
        void AssertEqual(string str, params LuaValue[] expected) {
            var f = Lua.Compile("return " + str);
            var s = new LuaState(16, 4);
            var g = new LuaTable();
            StdLib.Basic.Load(g);
            var closure = new LuaClosure(f, new []{new LuaUpValue { Value = g }});
            Assert.Equal(expected, s.Execute(closure));
        }

        [Fact]
        public void CollectGarbage() {
            AssertEqual("collectgarbage()");
            AssertEqual("collectgarbage('count')", 0);
            AssertEqual("collectgarbage('isrunning')", true);
        }

        [Fact]
        public void Print() {
            var prev = Console.Out;
            try {
                var sw = new StringWriter();
                Console.SetOut(sw);
                AssertEqual("print(1, 'foo')");
                Assert.Equal(sw.ToString(), "1\tfoo" + Environment.NewLine);
            } finally {
                Console.SetOut(prev);
            }
        }

        [Fact]
        public void Select() {
            AssertEqual("select('#', 1, 1, 1, 1, 1)", 5);
            AssertEqual("select(2, 'a', 'b', 'c')", "b");
        }

        [Fact]
        public void Next() {
            var l = new Lua();
            StdLib.Basic.Load(l.Globals);
            Assert.Equal(new LuaValue[]{6}, l.DoString("local x = 0; for k,v in next, {a=1, b=2, c=3} do x = x + v end; return x"));
        }

        [Fact]
        public void _G() {
            var l = new Lua();
            StdLib.Basic.Load(l.Globals);
            Assert.Equal(l.DoString("return _G"), new LuaValue[]{l.Globals});
        }

        [Fact]
        public void _VERSION() {
            AssertEqual("_VERSION", "Lua 5.4");
        }

        [Fact]
        public void Type() {
            AssertEqual("type(nil)", "nil");
            AssertEqual("type(true)", "boolean");
            AssertEqual("type(1)", "number");
            AssertEqual("type('foo')", "string");
            AssertEqual("type(print)", "function");
            AssertEqual("type({})", "table");
        }

        [Fact]
        public void PCall() {
            AssertEqual("pcall(function(a) return a end, 1)", true, 1);
            AssertEqual("pcall(function(a) error(a) end, 'foo')", false, "foo");
        }

        [Fact]
        public void Pairs() {
            var l = new Lua();
            StdLib.Basic.Load(l.Globals);
            Assert.Equal(new LuaValue[]{6}, l.DoString("local x = 0; for k,v in pairs({1, a=2, 3}) do x = x + v end; return x"));
        }

        [Fact]
        public void IPairs() {
            var l = new Lua();
            StdLib.Basic.Load(l.Globals);
            Assert.Equal(new LuaValue[]{6}, l.DoString("local x = 0; for k,v in ipairs({1, 2, 3, a=4}) do x = x + v end; return x"));
        }
    }
}
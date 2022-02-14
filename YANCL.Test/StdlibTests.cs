using System;
using System.IO;
using Xunit;

namespace YANCL.Test
{
    public class StdlibTests
    {
        void AssertEqual(string str, params LuaValue[] expected) {
            var f = Lua.Compile("return " + str);
            var s = new LuaState(16, 2);
            var g = new LuaTable();
            StdLib.Basic.Load(g);
            var closure = new LuaClosure(f, new []{new LuaUpValue { Value = g }});
            Assert.Equal(s.Execute(closure), expected);
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
            Assert.Equal(l.DoString("local x = 0; for k,v in next, {a=1, b=2, c=3} do x = x + v end; return x"), new LuaValue[]{6});
        }
    }
}
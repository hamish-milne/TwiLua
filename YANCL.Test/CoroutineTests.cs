using System;
using System.IO;
using Xunit;

namespace YANCL.Test
{
    public class CoroutineTests
    {
        void AssertEqual(string str, params LuaValue[] expected) {
            var f = Lua.Compile(str);
            var s = new LuaThread(isMain: true, 16, 4);
            var g = new LuaTable();
            StdLib.Coroutine.Load(g);
            var closure = new LuaClosure(f, new []{new LuaUpValue { Value = g }});
            Assert.Equal(expected, s.Execute(closure));
        }

        [Fact]
        public void Simple()
        {
            AssertEqual(@"
ret = ''
co = coroutine.create(function(x)
    ret = x..coroutine.yield()
    ret = ret..coroutine.yield()
end)
coroutine.resume(co, 'a')
coroutine.resume(co, 'b')
coroutine.resume(co, 'c')
return ret", "abc");
        }

        [Fact]
        public void Running()
        {
            AssertEqual("return coroutine.resume(coroutine.create(function() local a,b = coroutine.running(); return b end))", true, false);
            AssertEqual("local a,b = coroutine.running(); return b", true);
        }

        [Fact]
        public void IsYieldable()
        {
            AssertEqual("return coroutine.isyieldable()", false);
            AssertEqual("return coroutine.isyieldable(coroutine.create(function() end))", true);
        }

        [Fact]
        public void Status()
        {
            AssertEqual("return coroutine.status(coroutine.running())", "running");
            AssertEqual("return coroutine.status(coroutine.create(function() end))", "suspended");
            AssertEqual("co = coroutine.create(function() coroutine.yield() end); coroutine.resume(co); return coroutine.status(co)", "suspended");
            AssertEqual(@"
co = coroutine.create(function() end);
coroutine.resume(co);
return coroutine.status(co)", "dead");
            AssertEqual(@"
co1 = coroutine.create(function() status = coroutine.status(co2) end);
co2 = coroutine.create(function() coroutine.resume(co1) end);
coroutine.resume(co2);
return status", "normal");
        }
    }
}
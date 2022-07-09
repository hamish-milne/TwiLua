using System;
using System.IO;
using Xunit;

namespace YANCL.Test
{
    public class CoroutineTests
    {
        void AssertEqual(string str, params LuaValue[] expected) {
            var f = Lua.Compile(str);
            var s = new LuaThread(16, 4);
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
    }
}
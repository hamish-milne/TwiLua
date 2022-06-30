using System;
using System.Collections.Generic;
using Xunit;

namespace YANCL.Test
{
    public class Metamethods
    {
        [Fact]
        public void IndexFunction() {
            var s = new Lua();
            StdLib.Basic.Load(s.Globals);
            var results = s.DoString("t = setmetatable({'foo'}, { __index = function(t,k) return k+2 end }); return t[1], t[2]");
            Assert.Equal(new LuaValue[] { "foo", 4 }, results);
        }
    }
}
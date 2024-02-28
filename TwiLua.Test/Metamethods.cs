using System;
using System.Collections.Generic;
using Xunit;

namespace TwiLua.Test
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

        [Fact]
        public void NewIndexFunction() {
            var s = new Lua();
            StdLib.Basic.Load(s.Globals);
            var results = s.DoString("t = setmetatable({'foo'}, { __newindex = function(t,k,v) rawset(t, k, v+2) end }); t[2] = 2; return t[1], t[2]");
            Assert.Equal(new LuaValue[] { "foo", 4 }, results);
        }

        [Fact]
        public void AddFunction() {
            var s = new Lua();
            StdLib.Basic.Load(s.Globals);
            var results = s.DoString("t = setmetatable({}, { __add = function(t,v) return v..'bar', 'qux' end }); return 'foo' + t");
            Assert.Equal(new LuaValue[] { "foobar" }, results);
        }

        [Fact]
        public void BAndFunction() {
            var s = new Lua();
            StdLib.Basic.Load(s.Globals);
            var results = s.DoString("t = setmetatable({}, { __band = function(t,v) return v..'bar', 'qux' end }); return 'foo' & t");
            Assert.Equal(new LuaValue[] { "foobar" }, results);
        }
    }
}
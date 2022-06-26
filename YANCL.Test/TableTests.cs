using System;
using System.Collections.Generic;
using Xunit;

namespace YANCL.Test
{
    public class TableTests
    {
        [Fact]
        public void Concat() {
            var s = new Lua();
            StdLib.Table.Load(s.Globals);
            var results = s.DoString("return table.concat({'a', 'b', 'c'}, ';')");
            Assert.Equal("a;b;c", results[0]);
        }

        [Fact]
        public void Insert() {
            var s = new Lua();
            StdLib.Table.Load(s.Globals);
            var results = s.DoString("local t = {1, 2, 3}; table.insert(t, 4); return t");
            Assert.Equal(new LuaTable { 1, 2, 3, 4 }, results[0].Table!);
        }
    }
}
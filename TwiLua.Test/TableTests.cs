using TwiLua.StdLib;
using Xunit;

namespace TwiLua.Test
{
    public class TableTests
    {
        [Fact]
        public void Concat() {
            var s = new Lua().LoadTable();
            var results = s.DoString("return table.concat({'a', 'b', 'c'}, ';')");
            Assert.Equal("a;b;c", results[0]);
        }

        [Fact]
        public void Insert() {
            var s = new Lua().LoadTable();
            var results = s.DoString("local t = {1, 2, 3}; table.insert(t, 4); return t");
            Assert.Equal(new LuaTable { 1, 2, 3, 4 }, results[0].Table!);
        }

        [Fact]
        public void Move() {
            var s = new Lua().LoadTable();
            var results = s.DoString("local t = {1, 2, 3, 4, 5}; table.move(t, 2, 4, 1); return t");
            Assert.Equal(new LuaTable { 2, 3, 4, 4, 5 }, results[0].Table!);
        }

        [Fact]
        public void Pack() {
            var s = new Lua().LoadTable();
            var results = s.DoString("return table.pack(1, 2, 3)");
            Assert.Equal(new LuaTable { 1, 2, 3, { "n", 3 } }, results[0].Table!);
        }

        [Fact]
        public void Remove() {
            var s = new Lua().LoadTable();
            var results = s.DoString("local t = {1, 2, 3}; table.remove(t); return t");
            Assert.Equal(new LuaTable { 1, 2 }, results[0].Table!);
        }

        [Fact]
        public void Unpack() {
            var s = new Lua().LoadTable();
            var results = s.DoString("return table.unpack({1, 2, 3})");
            Assert.Equal(new LuaValue[] { 1, 2, 3 }, results);
        }

        [Fact]
        public void Sort() {
            var s = new Lua().LoadTable();
            var results = s.DoString("local t = {3, 2, 1}; table.sort(t); return table.unpack(t)");
            Assert.Equal(new LuaValue[] { 1, 2, 3 }, results);
        }

        [Fact]
        public void SortWithComparator() {
            var s = new Lua().LoadTable();
            var results = s.DoString("local t = {1, 2, 3}; table.sort(t, function(a, b) return a > b end); return table.unpack(t)");
            Assert.Equal(new LuaValue[] { 3, 2, 1 }, results);
        }

    }
}
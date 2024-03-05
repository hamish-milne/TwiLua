using TwiLua.StdLib;
using Xunit;

namespace TwiLua.Test
{
    public class StringTests
    {
        void AssertEqual(string str, params LuaValue[] expected) {
            var f = Lua.Compile("return " + str);
            var s = new LuaThread(isMain: true, 16, 2);
            var g = new Lua().LoadString().Globals;
            var closure = new LuaClosure(f, new []{new LuaUpValue { Value = g }});
            Assert.Equal(expected, s.Execute(closure));
        }

        [Fact]
        public void Byte() => AssertEqual("string.byte('abcdef', 2, 4)", 98, 99, 100);

        [Fact]
        public void Char() => AssertEqual("string.char(98, 99, 100)", "bcd");

        [Fact]
        public void Len() => AssertEqual("string.len('abcde')", 5);

        [Fact]
        public void Reverse() => AssertEqual("string.reverse('abcde')", "edcba");

        [Fact]
        public void Rep() => AssertEqual("string.rep('foo', 3, ';')", "foo;foo;foo");

        [Fact]
        public void Lower() => AssertEqual("string.lower('FOOBAR')", "foobar");

        [Fact]
        public void Upper() => AssertEqual("string.upper('foobar')", "FOOBAR");
    
    }
}
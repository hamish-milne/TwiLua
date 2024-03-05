using System;
using TwiLua.StdLib;
using Xunit;

namespace TwiLua.Test
{
    public class MathTests
    {
        void AssertEqual(string str, double expected) {
            var f = Lua.Compile("return " + str);
            var s = new LuaThread(isMain: true, 16, 2);
            var g = new Lua().LoadMath().Globals;
            var closure = new LuaClosure(f, new []{new LuaUpValue { Value = g }});
            Assert.True(Math.Abs(s.Execute(closure)[0].Number - expected) < 1e-10);
        }

        [Fact]
        public void Abs() => AssertEqual("math.abs(-2.3)", 2.3);

        [Fact]
        public void Acos() => AssertEqual("math.deg(math.acos(0.5))", 60);

        [Fact]
        public void Asin() => AssertEqual("math.deg(math.asin(0.5))", 30);

        [Fact]
        public void Atan() => AssertEqual("math.deg(math.atan(1))", 45);

        [Fact]
        public void Ceil() => AssertEqual("math.ceil(1.5)", 2);

        [Fact]
        public void Cos() => AssertEqual("math.cos(math.rad(60))", 0.5);

        [Fact]
        public void Floor() => AssertEqual("math.floor(1.5)", 1);

        [Fact]
        public void Exp() => AssertEqual("math.floor(math.exp(10))", 22026);

        [Fact]
        public void Sin() => AssertEqual("math.sin(math.rad(30))", 0.5);

        [Fact]
        public void Tan() => AssertEqual("math.tan(math.rad(45))", 1);

        [Fact]
        public void Sqrt() => AssertEqual("math.sqrt(4)", 2);
    }
}
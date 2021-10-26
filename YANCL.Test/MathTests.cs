using System;
using Xunit;

namespace YANCL.Test
{
    public class MathTests
    {
        void AssertEqual(string str, double expected) {
            var f = Compiler.Compile("return " + str);
            var s = new LuaState(16, 2);
            var g = new LuaTable();
            StdLib.Math.Load(g);
            Assert.True(Math.Abs(s.Execute(f, g)[0].Number - expected) < 1e-10);
        }

        [Fact]
        public void Abs() => AssertEqual("math.abs(0-2.3)", 2.3);

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
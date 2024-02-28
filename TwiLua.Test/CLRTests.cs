using System;
using System.IO;
using Xunit;

namespace TwiLua.Test
{
    public class CLRTests
    {
        [Fact]
        public void Import()
        {
            var l = new Lua();
            CLR.Load(l.Globals);
            Assert.Equal(new LuaValue[]{ 1.23 }, l.DoString(@"
                local Double = import('System.Double')
                return Double.Parse('1.23')
            "));
        }

        [Fact]
        public void Typeof()
        {
            var l = new Lua();
            CLR.Load(l.Globals);
            Assert.Equal(new LuaValue[]{ "System.Double" }, l.DoString(@"
                local Double = import('System.Double')
                return typeof(Double).FullName
            "));
        }

        [Fact]
        public void ToClr()
        {
            var l = new Lua();
            CLR.Load(l.Globals);
            Assert.Equal(new LuaValue[]{ "1.23" }, l.DoString(@"
                local Double = import('System.Double')
                return toClr(1.23, Double):ToString()
            "));
        }

        [Fact]
        public void FromClr()
        {
            var l = new Lua();
            CLR.Load(l.Globals);
            Assert.Equal(new LuaValue[]{ 1.23 }, l.DoString(@"
                local Double = import('System.Double')
                return fromClr(toClr(1.23, Double))
            "));
        }
    }
}
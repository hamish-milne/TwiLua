using System;
using System.IO;
using Xunit;

namespace YANCL.Test
{
    public class CLRTests
    {
        [Fact]
        public void Import()
        {
            var l = new Lua();
            CLR.Load(l.Globals, new[]{typeof(double).Assembly});
            Assert.Equal(new LuaValue[]{ 1.23 }, l.DoString(@"
                local Double = clr.import('System.Double')
                return Double.Parse('1.23')
            "));
        }

        [Fact]
        public void Typeof()
        {
            var l = new Lua();
            CLR.Load(l.Globals, new[]{typeof(double).Assembly});
            Assert.Equal(new LuaValue[]{ "System.Double" }, l.DoString(@"
                local Double = clr.import('System.Double')
                return clr.typeof(Double).FullName
            "));
        }
    }
}
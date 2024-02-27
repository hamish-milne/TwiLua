using System;
using System.IO;
using Xunit;

namespace YANCL.Test
{
    public class UserdataTests
    {
        public class TestClass
        {
            public int Property { get; set; }
            public int Field;
            public int Method(int a, int b) => a + b;
            public int this[int a] => a + 1;

            public TestClass(int a) {
                Field = a;
            }
        }

        [Fact]
        public void GetField() {
            var l = new Lua();
            l.Globals["foo"] = LuaValue.ConvertFrom(new TestClass(42));
            Assert.Equal(new LuaValue[]{42}, l.DoString("return foo.Field"));
        }
    }
}
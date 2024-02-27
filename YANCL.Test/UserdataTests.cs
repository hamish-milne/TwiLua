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
            public int this[int a] {
                get => a + 1;
                set => Field = value;
            }

            public TestClass(int a) {
                Field = a;
            }

            public static int StaticMethod(int a, int b) => a + b;
            public static int StaticField;
            public static int StaticProperty { get; set; }
        }

        [Fact]
        public void GetField() {
            var l = new Lua();
            l.Globals["foo"] = LuaValue.ConvertFrom(new TestClass(42));
            Assert.Equal(new LuaValue[]{42}, l.DoString("return foo.Field"));
        }

        [Fact]
        public void SetField() {
            var l = new Lua();
            var obj = new TestClass(42);
            l.Globals["foo"] = LuaValue.ConvertFrom(obj);
            l.DoString("foo.Field = 43");
            Assert.Equal(43, obj.Field);
        }

        [Fact]
        public void GetProperty() {
            var l = new Lua();
            l.Globals["foo"] = LuaValue.ConvertFrom(new TestClass(0) { Property = 42 });
            Assert.Equal(new LuaValue[]{42}, l.DoString("return foo.Property"));
        }

        [Fact]
        public void SetProperty() {
            var l = new Lua();
            var obj = new TestClass(0);
            l.Globals["foo"] = LuaValue.ConvertFrom(obj);
            l.DoString("foo.Property = 43");
            Assert.Equal(43, obj.Property);
        }

        [Fact]
        public void CallMethod() {
            var l = new Lua();
            l.Globals["foo"] = LuaValue.ConvertFrom(new TestClass(0));
            Assert.Equal(new LuaValue[]{3}, l.DoString("return foo:Method(1, 2)"));
        }

        [Fact]
        public void IndexerGet() {
            var l = new Lua();
            l.Globals["foo"] = LuaValue.ConvertFrom(new TestClass(0));
            Assert.Equal(new LuaValue[]{2}, l.DoString("return foo[1]"));
        }

        [Fact]
        public void IndexerSet() {
            var l = new Lua();
            var obj = new TestClass(0);
            l.Globals["foo"] = LuaValue.ConvertFrom(obj);
            l.DoString("foo[1] = 3");
            Assert.Equal(3, obj.Field);
        }

        [Fact]
        public void Constructor() {
            var l = new Lua();
            l.Globals["TestClass"] = TypeUserdata.From(typeof(TestClass));
            Assert.Equal(new LuaValue[]{42}, l.DoString("return TestClass(42).Field"));
        }

        [Fact]
        public void GetStaticField() {
            var l = new Lua();
            l.Globals["TestClass"] = TypeUserdata.From(typeof(TestClass));
            TestClass.StaticField = 69;
            Assert.Equal(new LuaValue[]{69}, l.DoString("return TestClass.StaticField"));
        }

        [Fact]
        public void SetStaticField() {
            var l = new Lua();
            l.Globals["TestClass"] = TypeUserdata.From(typeof(TestClass));
            l.DoString("TestClass.StaticField = 70");
            Assert.Equal(70, TestClass.StaticField);
        }

        [Fact]
        public void GetStaticProperty() {
            var l = new Lua();
            l.Globals["TestClass"] = TypeUserdata.From(typeof(TestClass));
            TestClass.StaticProperty = 69;
            Assert.Equal(new LuaValue[]{69}, l.DoString("return TestClass.StaticProperty"));
        }

        [Fact]
        public void SetStaticProperty() {
            var l = new Lua();
            l.Globals["TestClass"] = TypeUserdata.From(typeof(TestClass));
            l.DoString("TestClass.StaticProperty = 70");
            Assert.Equal(70, TestClass.StaticProperty);
        }

        [Fact]
        public void CallStaticMethod() {
            var l = new Lua();
            l.Globals["TestClass"] = TypeUserdata.From(typeof(TestClass));
            Assert.Equal(new LuaValue[]{3}, l.DoString("return TestClass.StaticMethod(1, 2)"));
        }
    }
}
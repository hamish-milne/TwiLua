using System;
using Xunit;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL.Test
{
    public class CompilerTests
    {
        [Fact]
        public void GlobalAssignment()
        {
            var code = @"a = 5";
            var result = Compiler.Compile(code);
            Assert.Equal(new []{
                new LuaValue("a"),
                new LuaValue(5),
            }, result.constants);
            Assert.Equal(new []{
                Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
            }, result.code);
        }
    }
}
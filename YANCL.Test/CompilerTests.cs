using System;
using Xunit;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL.Test
{
    public class CompilerTests
    {
        void DoCompilerTest(
            string source,
            LuaValue[] expectedConstants,
            int[] expectedInstructions
        ) {
            var result = Compiler.Compile(source);
            Assert.Equal(expectedConstants, result.constants);
            Assert.Equal(expectedInstructions, result.code);
        }

        [Fact]
        public void GlobalAssignment()
        {
            DoCompilerTest(
                "x = 10",
                new [] { new LuaValue("x"), new LuaValue(10) },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                }
            );
        }

        [Fact]
        public void MultipleStatements()
        {
            DoCompilerTest(
                "x = 10; y = 20",
                new [] { new LuaValue("x"), new LuaValue(10), new LuaValue("y"), new LuaValue(20) },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                    Build3(SETTABUP, 0, 2 | KFlag, 3 | KFlag),
                }
            );
        }

        [Fact]
        public void FunctionCall()
        {
            DoCompilerTest(
                "foo()",
                new [] { new LuaValue("foo") },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(CALL, 0, 1, 1),
                }
            );
        }

        [Fact]
        public void FunctionCallWithArguments()
        {
            DoCompilerTest(
                "print(10, 20)",
                new [] { new LuaValue("print"), new LuaValue(10), new LuaValue(20) },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2x(LOADK, 1, 1),
                    Build2x(LOADK, 2, 2),
                    Build3(CALL, 0, 3, 1),
                }
            );
        }

        [Fact]
        public void MemberRead()
        {
            DoCompilerTest(
                "x = y.z",
                new [] { new LuaValue("x"), new LuaValue("y"), new LuaValue("z") },
                new [] {
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(GETTABLE, 0, 0, 2 | KFlag),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                }
            );
        }

        [Fact]
        public void MemberWrite()
        {
            DoCompilerTest(
                "x.y = z",
                new [] { new LuaValue("x"), new LuaValue("y"), new LuaValue("z") },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABUP, 1, 0, 2 | KFlag),
                    Build3(SETTABLE, 0, 1 | KFlag, 1),
                }
            );
        }
    }
}
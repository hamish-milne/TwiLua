using System;
using Xunit;
using static YANCL.Instruction;
using static YANCL.OpCode;
using System.Linq;

namespace YANCL.Test
{
    public class CompilerTests
    {
        void DoCompilerTest(
            string source,
            LuaValue[] expectedConstants,
            int[] expectedInstructions,
            int nLocals, int nSlots
        ) {
            var result = Compiler.Compile(source);
            Assert.Equal(expectedConstants, result.constants);
            if (!expectedInstructions.SequenceEqual(result.code)) {
                foreach (var pair in expectedInstructions.Zip(result.code)) {
                    if (pair.Item1 != pair.Item2) {
                        throw new Exception($"Expected {Stringify(pair.Item1)}\n but got {Stringify(pair.Item2)}");
                    }
                }
            }
            Assert.Equal(nLocals, result.nLocals);
            Assert.Equal(nSlots, result.nSlots);
        }

        [Fact]
        public void GlobalAssignment()
        {
            DoCompilerTest(
                "x = 10",
                new LuaValue[] { "x", 10 },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                },
                0, 0
            );
        }

        [Fact]
        public void MultipleStatements()
        {
            DoCompilerTest(
                "x = 10; y = 20",
                new LuaValue[] { "x", 10, "y", 20 },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                    Build3(SETTABUP, 0, 2 | KFlag, 3 | KFlag),
                },
                0, 0
            );
        }

        [Fact]
        public void FunctionCall()
        {
            DoCompilerTest(
                "foo()",
                new LuaValue[] { "foo" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(CALL, 0, 1, 1),
                },
                0, 1
            );
        }

        [Fact]
        public void FunctionCallWithArguments()
        {
            DoCompilerTest(
                "print(10, 20)",
                new LuaValue[] { "print", 10, 20 },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2x(LOADK, 1, 1),
                    Build2x(LOADK, 2, 2),
                    Build3(CALL, 0, 3, 1),
                },
                0, 3
            );
        }

        [Fact]
        public void MemberRead()
        {
            DoCompilerTest(
                "x = y.z",
                new LuaValue[] { "x", "y", "z" },
                new [] {
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(GETTABLE, 0, 0, 2 | KFlag),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 1
            );
        }

        [Fact]
        public void MemberWrite()
        {
            DoCompilerTest(
                "x.y = z",
                new LuaValue[] { "x", "y", "z" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABUP, 1, 0, 2 | KFlag),
                    Build3(SETTABLE, 0, 1 | KFlag, 1),
                },
                0, 2
            );
        }

        [Fact]
        public void ParenthesizedPrefix()
        {
            DoCompilerTest(
                "(x).y = z",
                new LuaValue[] { "x", "y", "z" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABUP, 1, 0, 2 | KFlag),
                    Build3(SETTABLE, 0, 1 | KFlag, 1),
                },
                0, 2
            );
        }

        [Fact]
        public void IndexWrite()
        {
            DoCompilerTest(
                "x[1] = 2",
                new LuaValue[] { "x", 1, 2 },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(SETTABLE, 0, 1 | KFlag, 2 | KFlag),
                },
                0, 1
            );
        }

        [Fact]
        public void CallAssignment()
        {
            DoCompilerTest(
                "x().y = z",
                new LuaValue[] { "x", "y", "z" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(CALL, 0, 1, 2),
                    Build3(GETTABUP, 1, 0, 2 | KFlag),
                    Build3(SETTABLE, 0, 1 | KFlag, 1),
                },
                0, 2
            );
        }

        [Fact]
        public void CallExpression()
        {
            DoCompilerTest(
                "x = y(z)",
                new LuaValue[] { "x", "y", "z" },
                new [] {
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(GETTABUP, 1, 0, 2 | KFlag),
                    Build3(CALL, 0, 2, 2),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 2
            );
        }

        [Fact]
        public void CallTableSyntax()
        {
            DoCompilerTest(
                "x {}",
                new LuaValue[] { "x" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(NEWTABLE, 1, 0, 0),
                    Build3(CALL, 0, 2, 1),
                },
                0, 2
            );
        }

        [Fact]
        public void CallStringSyntax()
        {
            DoCompilerTest(
                "x 'foo'",
                new LuaValue[] { "x", "foo" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2x(LOADK, 1, 1),
                    Build3(CALL, 0, 2, 1),
                },
                0, 2
            );
        }

        [Fact]
        public void SelfCall()
        {
            DoCompilerTest(
                "x:y(1, 2)",
                new LuaValue[] { "x", "y", 1, 2 },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(SELF, 0, 0, 1 | KFlag),
                    Build2x(LOADK, 2, 2 | KFlag),
                    Build2x(LOADK, 3, 3 | KFlag),
                    Build3(CALL, 0, 4, 1),
                },
                0, 4
            );
        }

        [Fact]
        public void GlobalLiterals()
        {
            DoCompilerTest(
                "x = true; y = false; z = nil",
                new LuaValue[] { "x", true, "y", false, "z", LuaValue.Nil },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                    Build3(SETTABUP, 0, 1 | KFlag, 2 | KFlag),
                    Build3(SETTABUP, 0, 3 | KFlag, 4 | KFlag),
                },
                0, 0
            );
        }

        [Fact]
        public void LocalLiterals()
        {
            DoCompilerTest(
                "local x = true; local y = false; local z = nil",
                new LuaValue[] { "x", "y", "z" },
                new [] {
                    Build2(LOADBOOL, 0, 1),
                    Build2(LOADBOOL, 1, 0),
                    Build2(LOADNIL, 2, 0),
                },
                3, 0
            );
        }

        [Fact]
        public void StringLiterals()
        {
            DoCompilerTest(
                "x = 'foo'; y = \"bar\"",
                new LuaValue[] { "x", "foo", "y", "bar" },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                    Build3(SETTABUP, 0, 2 | KFlag, 3 | KFlag),
                },
                0, 0
            );
        }

        [Fact]
        public void NumericLiterals()
        {
            DoCompilerTest(
                "x = 1; y = 2.0; z = 3e4",
                new LuaValue[] { "x", 1, "y", 2.0, "z", 3e4 },
                new [] {
                    Build3(SETTABUP, 0, 0 | KFlag, 1 | KFlag),
                    Build3(SETTABUP, 0, 2 | KFlag, 3 | KFlag),
                    Build3(SETTABUP, 0, 4 | KFlag, 5 | KFlag),
                },
                0, 0
            );
        }

        [Fact]
        public void ExpressionChain()
        {
            DoCompilerTest(
                "x = (y()).z['w'](v)",
                new LuaValue[] { "x", "y", "z", "w", "v" },
                new [] {
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(CALL, 0, 1, 2),
                    Build3(GETTABLE, 0, 0, 2 | KFlag),
                    Build3(GETTABLE, 0, 0, 3 | KFlag),
                    Build3(GETTABUP, 1, 0, 4 | KFlag),
                    Build3(CALL, 0, 2, 2),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 2
            );
        }

        [Fact]
        public void TableConstructor()
        {
            DoCompilerTest(
                "x = { 1, 2, 3 }",
                new LuaValue[] { "x", 1, 2, 3 },
                new [] {
                    Build3(NEWTABLE, 0, 3, 0),
                    Build2x(LOADK, 1, 1 | KFlag),
                    Build2x(LOADK, 2, 2 | KFlag),
                    Build2x(LOADK, 3, 3 | KFlag),
                    Build3(SETLIST, 0, 3, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 4
            );
        }

        [Fact]
        public void LocalVariable()
        {
            DoCompilerTest(
                "local x; x = 1",
                new LuaValue[] { "x", 1 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build2(LOADK, 1, 1 | KFlag),
                },
                1, 0
            );
        }

        [Fact]
        public void MultipleAssignmentGlobal()
        {
            DoCompilerTest(
                "x, y = 1, 2",
                new LuaValue[] { "x", "y", 1, 2 },
                new [] {
                    Build2x(LOADK, 0, 2),
                    Build3(SETTABUP, 0, 1 | KFlag, 3 | KFlag),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 1
            );
        }

        [Fact]
        public void MultipleAssignmentWithCall()
        {
            DoCompilerTest(
                "x, y, z = 1, w()",
                new LuaValue[] { "x", "y", "z", 1, "w" },
                new [] {
                    Build2x(LOADK, 0, 3 | KFlag),
                    Build3(GETTABUP, 1, 0, 4 | KFlag),
                    Build3(CALL, 1, 1, 3),
                    Build3(SETTABUP, 0, 2 | KFlag, 2),
                    Build3(SETTABUP, 0, 1 | KFlag, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 3
            );
        }

        [Fact]
        public void MultipleAssignmentLocal()
        {
            DoCompilerTest(
                "local x, y = 1, 2",
                new LuaValue[] { "x", "y", 1, 2 },
                new [] {
                    Build2(LOADK, 0, 0 | KFlag),
                    Build2(LOADK, 1, 1 | KFlag),
                },
                2, 0
            );
        }

        [Fact]
        public void MultipleAssignmentMixed()
        {
            DoCompilerTest(
                "local x; x, y = 1, 2",
                new LuaValue[] { "x", "y", 1, 2 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build2(LOADK, 1, 1 | KFlag),
                    Build3(SETTABUP, 0, 0 | KFlag, 2 | KFlag),
                    Build2(MOVE, 0, 1),
                },
                1, 2
            );
        }

        [Fact]
        public void MultipleAssignmentOverflow()
        {
            DoCompilerTest(
                "x, y = a, b, c",
                new LuaValue[] { "x", "y", "a", "b", "c" },
                new [] {
                    Build3(GETTABUP, 0, 0, 2 | KFlag),
                    Build3(GETTABUP, 1, 0, 3 | KFlag),
                    Build3(GETTABUP, 2, 0, 4 | KFlag),
                    Build3(SETTABUP, 0, 1 | KFlag, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 3
            );
        }

        [Fact]
        public void MultipleAssignmentUnderflow()
        {
            DoCompilerTest(
                "x, y, z = a, b",
                new LuaValue[] { "x", "y", "z", "a", "b" },
                new [] {
                    Build3(GETTABUP, 0, 0, 3 | KFlag),
                    Build3(GETTABUP, 1, 0, 4 | KFlag),
                    Build2(LOADNIL, 2, 0),
                    Build3(SETTABUP, 0, 2 | KFlag, 2),
                    Build3(SETTABUP, 0, 1 | KFlag, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 3
            );
        }
    }
}
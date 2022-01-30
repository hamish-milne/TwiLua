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
            int nLocals, int nSlots,
            (LuaValue[] constants, int[] instructions, int nLocals, int nSlots)[] functions = null
        ) {
            var result = Parser.Compile(source);
            Assert.Equal(expectedConstants, result.constants);
            Assert.Equal(expectedInstructions.Length, result.code.Length);
            if (!expectedInstructions.SequenceEqual(result.code)) {
                foreach (var pair in expectedInstructions.Zip(result.code)) {
                    if (pair.Item1 != pair.Item2) {
                        throw new Exception($"Expected {Stringify(pair.Item1)}\n but got {Stringify(pair.Item2)}");
                    }
                }
            }
            Assert.Equal(nLocals, result.nLocals);
            Assert.Equal(nSlots, result.nSlots);
            if (functions != null) {
                Assert.Equal(functions.Length, result.prototypes.Length);
                for (int i = 0; i < functions.Length; i++) {
                    var (expectedConstants1, expectedInstructions1, expectedNLocals, expectedNSlots) = functions[i];
                    Assert.Equal(expectedConstants1, result.prototypes[i].constants);
                    foreach (var pair in expectedInstructions1.Zip(result.prototypes[i].code)) {
                        if (pair.Item1 != pair.Item2) {
                            throw new Exception($"Expected {Stringify(pair.Item1)}\n but got {Stringify(pair.Item2)}");
                        }
                    }
                    Assert.Equal(expectedNLocals, result.prototypes[i].nLocals);
                    Assert.Equal(expectedNSlots, result.prototypes[i].nSlots);
                }
            }
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
        public void LocalAssignment()
        {
            DoCompilerTest(
                "local x; x = y",
                new LuaValue[] { "y" },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                },
                1, 0
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
        public void LocalMemberAssignment()
        {
            DoCompilerTest(
                "local x; x.y.z = 1",
                new LuaValue[] { "y", "z", 1 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build3(GETTABLE, 1, 0, 0 | KFlag),
                    Build3(SETTABLE, 1, 1 | KFlag, 2 | KFlag),
                },
                1, 1
            );
        }

        [Fact]
        public void LocalCallAssignment()
        {
            DoCompilerTest(
                "local x; x(a)[b] = 1",
                new LuaValue[] { "a", "b", 1 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build2(MOVE, 1, 0),
                    Build3(GETTABUP, 2, 0, 0 | KFlag),
                    Build3(CALL, 1, 2, 2),
                    Build3(GETTABUP, 2, 0, 1 | KFlag),
                    Build3(SETTABLE, 1, 2, 2 | KFlag),
                },
                1, 2
            );
        }

        [Fact]
        public void LocalIndexAssignment()
        {
            DoCompilerTest(
                "local x; x[y][z] = 1",
                new LuaValue[] { "y", "z", 1 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build3(GETTABUP, 1, 0, 0 | KFlag),
                    Build3(GETTABLE, 1, 0, 1),
                    Build3(GETTABUP, 2, 0, 1 | KFlag),
                    Build3(SETTABLE, 1, 2, 2 | KFlag),
                },
                1, 2
            );
        }

        [Fact]
        public void LocalMemberRead()
        {
            DoCompilerTest(
                "local x; a = x.y.z",
                new LuaValue[] { "a", "y", "z" },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build3(GETTABLE, 1, 0, 1 | KFlag),
                    Build3(GETTABLE, 1, 1, 2 | KFlag),
                    Build3(SETTABUP, 0, 0 | KFlag, 1),
                },
                1, 1
            );
        }

        [Fact]
        public void LocalIndexRead()
        {
            DoCompilerTest(
                "local x; a = x[y][z]",
                new LuaValue[] { "a", "y", "z" },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build3(GETTABUP, 1, 0, 1 | KFlag),
                    Build3(GETTABLE, 1, 0, 1),
                    Build3(GETTABUP, 2, 0, 2 | KFlag),
                    Build3(GETTABLE, 1, 1, 2),
                    Build3(SETTABUP, 0, 0 | KFlag, 1),
                },
                1, 2
            );
        }

        [Fact]
        public void LocalCall()
        {
            DoCompilerTest(
                "local x; a = x(y)",
                new LuaValue[] { "a", "y" },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build2(MOVE, 1, 0),
                    Build3(GETTABUP, 2, 0, 1 | KFlag),
                    Build3(CALL, 1, 2, 2),
                    Build3(SETTABUP, 0, 0 | KFlag, 1),
                },
                1, 2
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
                    Build2x(LOADK, 2, 2),
                    Build2x(LOADK, 3, 3),
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
                    Build3(SETTABUP, 0, 2 | KFlag, 3 | KFlag),
                    Build3(SETTABUP, 0, 4 | KFlag, 5 | KFlag),
                },
                0, 0
            );
        }

        [Fact]
        public void LocalLiterals()
        {
            DoCompilerTest(
                "local x = true; local y = false; local z = nil",
                new LuaValue[] { },
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
        public void SimpleTableConstructor()
        {
            DoCompilerTest(
                "x = { 1, 2, 3 }",
                new LuaValue[] { "x", 1, 2, 3 },
                new [] {
                    Build3(NEWTABLE, 0, 3, 0),
                    Build2x(LOADK, 1, 1),
                    Build2x(LOADK, 2, 2),
                    Build2x(LOADK, 3, 3),
                    Build3(SETLIST, 0, 3, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 4
            );
        }

        [Fact]
        public void ComplexTableConstructor()
        {
            DoCompilerTest(
                "x = { 1, 2, 3, a = 4, [b] = 5, ['c'] = 6, 7 }",
                new LuaValue[] { "x", 1, 2, 3, "a", 4, "b", 5, "c", 6, 7 },
                new [] {
                    Build3(NEWTABLE, 0, 4, 3),
                    Build2x(LOADK, 1, 1),
                    Build2x(LOADK, 2, 2),
                    Build2x(LOADK, 3, 3),
                    Build3(SETTABLE, 0, 4 | KFlag, 5 | KFlag),
                    Build3(GETTABUP, 4, 0, 6 | KFlag),
                    Build3(SETTABLE, 0, 4, 7 | KFlag),
                    Build3(SETTABLE, 0, 8 | KFlag, 9 | KFlag),
                    Build2x(LOADK, 4, 10),
                    Build3(SETLIST, 0, 4, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 5
            );
        }

        [Fact]
        public void LocalVariable()
        {
            DoCompilerTest(
                "local x; x = 1",
                new LuaValue[] { 1 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build2x(LOADK, 0, 0),
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
                    Build2x(LOADK, 0, 3),
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
                new LuaValue[] { 1, 2 },
                new [] {
                    Build2x(LOADK, 0, 0),
                    Build2x(LOADK, 1, 1),
                },
                2, 0
            );
        }

        [Fact]
        public void MultipleAssignmentMixed()
        {
            DoCompilerTest(
                "local x; x, y = 1, 2",
                new LuaValue[] { "y", 1, 2 },
                new [] {
                    Build2(LOADNIL, 0, 0),
                    Build2x(LOADK, 1, 1),
                    Build3(SETTABUP, 0, 0 | KFlag, 2 | KFlag),
                    Build2(MOVE, 0, 1),
                },
                1, 1
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

        [Fact]
        public void FunctionCallDispatch()
        {
            DoCompilerTest(
                "foo(1, 2, bar())",
                new LuaValue[] { "foo", 1, 2, "bar" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2x(LOADK, 1, 1),
                    Build2x(LOADK, 2, 2),
                    Build3(GETTABUP, 3, 0, 3 | KFlag),
                    Build3(CALL, 3, 1, 0),
                    Build3(CALL, 0, 0, 1),
                },
                0, 4
            );
        }

        [Fact]
        public void VarargDispatch()
        {
            DoCompilerTest(
                "foo(1, 2, ...)",
                new LuaValue[] { "foo", 1, 2 },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2x(LOADK, 1, 1),
                    Build2x(LOADK, 2, 2),
                    Build2(VARARG, 3, 0),
                    Build3(CALL, 0, 0, 1),
                },
                0, 4
            );
        }

        [Fact]
        public void VarargExpression()
        {
            DoCompilerTest(
                "local a = (...)[...]",
                new LuaValue[] { },
                new [] {
                    Build2(VARARG, 0, 2),
                    Build2(VARARG, 1, 2),
                    Build3(GETTABLE, 0, 0, 1),
                },
                1, 1
            );
        }

        [Fact]
        public void VarargAssignment()
        {
            DoCompilerTest(
                "local a, b, c = 1, ...",
                new LuaValue[] { 1 },
                new [] {
                    Build2(LOADK, 0, 0),
                    Build2(VARARG, 1, 3),
                },
                3, 0
            );
        }

        [Fact]
        public void VarargTableConstructor()
        {
            DoCompilerTest(
                "local a, b = {c()}, {...}",
                new LuaValue[] { "c" },
                new [] {
                    Build3(NEWTABLE, 0, 0, 0),
                    Build3(GETTABUP, 1, 0, 0 | KFlag),
                    Build3(CALL, 1, 1, 0),
                    Build3(SETLIST, 0, 0, 1),
                    Build3(NEWTABLE, 1, 0, 0),
                    Build2(VARARG, 2, 0),
                    Build3(SETLIST, 1, 0, 1),
                },
                2, 1
            );
        }

        [Fact]
        public void SimpleClosure()
        {
            DoCompilerTest(
                "local a = function (a) print(b) end",
                new LuaValue[] { },
                new [] {
                    Build2(CLOSURE, 0, 0),
                },
                1, 0,
                new [] {(
                    new LuaValue[] { "print", "b" },
                    new [] {
                        Build3(GETTABUP, 1, 0, 0 | KFlag),
                        Build3(GETTABUP, 2, 0, 1 | KFlag),
                        Build3(CALL, 1, 2, 1),
                    },
                    1, 2
                )}
            );
        }

        [Fact]
        public void LocalFunction()
        {
            DoCompilerTest(
                "local function foo() end",
                new LuaValue[] { },
                new [] {
                    Build2(CLOSURE, 0, 0),
                },
                1, 0,
                new [] {(
                    new LuaValue[] { },
                    new int[] { },
                    0, 0
                )}
            );
        }

        [Fact]
        public void GlobalFunction()
        {
            DoCompilerTest(
                "function foo() end",
                new LuaValue[] { "foo" },
                new [] {
                    Build2(CLOSURE, 0, 0),
                    Build3(SETTABUP, 0, 0 | KFlag, 0),
                },
                0, 1,
                new [] {(
                    new LuaValue[] { },
                    new int[] { },
                    0, 0
                )}
            );
        }

        [Fact]
        public void MemberFunction()
        {
            DoCompilerTest(
                "function a.b.c() end",
                new LuaValue[] { "a", "b", "c" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABLE, 0, 0, 1 | KFlag),
                    Build2(CLOSURE, 1, 0),
                    Build3(SETTABLE, 0, 2 | KFlag, 1),
                },
                0, 2,
                new [] {(
                    new LuaValue[] { },
                    new int[] { },
                    0, 0
                )}
            );
        }

        [Fact]
        public void SelfFunction()
        {
            DoCompilerTest(
                "function a.b:c() print(self) end",
                new LuaValue[] { "a", "b", "c" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABLE, 0, 0, 1 | KFlag),
                    Build2(CLOSURE, 1, 0),
                    Build3(SETTABLE, 0, 2 | KFlag, 1),
                },
                0, 2,
                new [] {(
                    new LuaValue[] { "print" },
                    new [] {
                        Build3(GETTABUP, 1, 0, 0 | KFlag),
                        Build2(MOVE, 2, 0),
                        Build3(CALL, 1, 2, 1),
                    },
                    1, 2
                )}
            );
        }

        [Fact]
        public void Length()
        {
            DoCompilerTest(
                "local a = #x",
                new LuaValue[] { "x" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2(LEN, 0, 0)
                },
                1, 0
            );
        }

        [Fact]
        public void LogicalNot1()
        {
            DoCompilerTest(
                "local a = not x",
                new LuaValue[] { "x" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2(NOT, 0, 0)
                },
                1, 0
            );
        }

        [Fact]
        public void LogicalNot2()
        {
            DoCompilerTest(
                "local a = not (x < y)",
                new LuaValue[] { "x", "y" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABUP, 1, 0, 1 | KFlag),
                    Build3(LT, 0, 0, 1),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 0, 0, 1),
                    Build3(LOADBOOL, 0, 1, 0),
                },
                1, 1
            );
        }

        [Fact]
        public void LogicalNot3()
        {
            DoCompilerTest(
                "if not x then y() end",
                new LuaValue[] { "x", "y" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(TEST, 0, 0, 1),
                    Build2sx(JMP, 0, 2),
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(CALL, 0, 1, 1),
                },
                0, 1
            );
        }
        

        [Fact]
        public void UnaryMinus()
        {
            DoCompilerTest(
                "local a = -x",
                new LuaValue[] { "x" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2(UNM, 0, 0)
                },
                1, 0
            );
        }

        [Fact]
        public void BitwiseNot()
        {
            DoCompilerTest(
                "local a = ~x",
                new LuaValue[] { "x" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build2(BNOT, 0, 0)
                },
                1, 0
            );
        }

        [Fact]
        public void SimpleMath()
        {
            DoCompilerTest(
                "local a,b,c,d,e; a = b * c / d + e",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(MUL, 5, 1, 2),
                    Build3(DIV, 5, 5, 3),
                    Build3(ADD, 0, 5, 4),
                },
                5, 1
            );
        }

        [Fact]
        public void OperatorPrecedence1()
        {
            DoCompilerTest(
                "local a,b,c,d,e; a = b + c / d * e",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(DIV, 5, 2, 3),
                    Build3(MUL, 5, 5, 4),
                    Build3(ADD, 0, 1, 5),
                },
                5, 1
            );
        }

        [Fact]
        public void ConstantFolding()
        {
            DoCompilerTest(
                "local a = 1 + 2 * 3 - 4",
                new LuaValue[] { 3 },
                new [] {
                    Build2x(LOADK, 0, 0),
                },
                1, 0
            );
        }

        [Fact]
        public void OperatorPrecedence2()
        {
            DoCompilerTest(
                "local a, b, c; a = #b^c",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 2),
                    Build3(POW, 3, 1, 2),
                    Build2(LEN, 0, 3),
                },
                3, 1
            );
        }

        [Fact]
        public void MixedStackOperations()
        {
            DoCompilerTest(
                "local a, c; a = #b^c",
                new LuaValue[] { "b" },
                new [] {
                    Build2(LOADNIL, 0, 1),
                    Build3(GETTABUP, 2, 0, 0 | KFlag),
                    Build3(POW, 2, 2, 1),
                    Build2(LEN, 0, 2),
                },
                2, 1
            );
        }

        [Fact]
        public void BitwiseOperators()
        {
            DoCompilerTest(
                "local a,b,c,d,e; a = b & c ~ d | e",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(BAND, 5, 1, 2),
                    Build3(BXOR, 5, 5, 3),
                    Build3(BOR, 0, 5, 4),
                },
                5, 1
            );
        }

        [Fact]
        public void ShiftOperators()
        {
            DoCompilerTest(
                "local a,b,c,d; a = b << c >> d",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 3),
                    Build3(SHL, 4, 1, 2),
                    Build3(SHR, 0, 4, 3),
                },
                4, 1
            );
        }

        [Fact]
        public void ComparisonOperators()
        {
            DoCompilerTest(
                "local a,b,c,d,e,f,g,h; a = b < c > d <= e >= f == g ~= h",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 7),
                    Build3(LT, 1, 1, 2),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 8, 0, 1),
                    Build3(LOADBOOL, 8, 1, 0),
                    Build3(LT, 1, 3, 8),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 8, 0, 1),
                    Build3(LOADBOOL, 8, 1, 0),
                    Build3(LE, 1, 8, 4),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 8, 0, 1),
                    Build3(LOADBOOL, 8, 1, 0),
                    Build3(LE, 1, 5, 8),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 8, 0, 1),
                    Build3(LOADBOOL, 8, 1, 0),
                    Build3(EQ, 1, 8, 6),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 8, 0, 1),
                    Build3(LOADBOOL, 8, 1, 0),
                    Build3(EQ, 0, 8, 7),
                    Build2sx(JMP, 0, 1),
                    Build3(LOADBOOL, 0, 0, 1),
                    Build3(LOADBOOL, 0, 1, 0),
                },
                8, 1
            );
        }

        [Fact]
        public void LogicalOperators1()
        {
            DoCompilerTest(
                "local a,b,c,d,x; x = a and b + c or d",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(TEST, 0, 0, 0),
                    Build2sx(JMP, 0, 3),
                    Build3(ADD, 5, 1, 2),
                    Build3(TESTSET, 4, 5, 1),
                    Build2sx(JMP, 0, 1),
                    Build2(MOVE, 4, 3),
                },
                5, 1
            );
        }

        [Fact]
        public void LogicalOperators2()
        {
            DoCompilerTest(
                "local a,b,c,d,x; x = a or b + c and d",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(TESTSET, 4, 0, 1),
                    Build2sx(JMP, 0, 4),
                    Build3(ADD, 5, 1, 2),
                    Build3(TESTSET, 4, 5, 0),
                    Build2sx(JMP, 0, 1),
                    Build2(MOVE, 4, 3),
                },
                5, 1
            );
        }

        [Fact]
        public void LogicalOperators3()
        {
            DoCompilerTest(
                "local a,b,c,d,x; x = (a or b) and (c or d)",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(TEST, 0, 0, 1),
                    Build2sx(JMP, 0, 2),
                    Build3(TESTSET, 4, 1, 0),
                    Build2sx(JMP, 0, 3),
                    Build3(TESTSET, 4, 2, 1),
                    Build2sx(JMP, 0, 1),
                    Build2(MOVE, 4, 3),
                },
                5, 1
            );
        }

        [Fact]
        public void LogicalOperatorsWithComparison()
        {
            DoCompilerTest(
                "local a,b,c,d,x; x = a or b < c and d",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 4),
                    Build3(TESTSET, 4, 0, 1),
                    Build2sx(JMP, 0, 6),
                    Build3(LT, 0, 1, 2),
                    Build2sx(JMP, 0, 2),
                    Build2(MOVE, 4, 3),
                    Build2sx(JMP, 0, 2),
                    Build3(LOADBOOL, 4, 0, 1),
                    Build3(LOADBOOL, 4, 1, 0),
                    Build2(MOVE, 4, 3),
                },
                5, 1
            );
        }

        [Fact]
        public void Concatenation()
        {
            DoCompilerTest(
                "local a,b,c,d,e,f,g; a = b..c..d + e..f..g",
                new LuaValue[] { },
                new [] {
                    Build2(LOADNIL, 0, 6),
                    Build2(MOVE, 7, 1),
                    Build2(MOVE, 8, 2),
                    Build3(ADD, 9, 3, 4),
                    Build2(MOVE, 10, 5),
                    Build2(MOVE, 11, 6),
                    Build3(CONCAT, 0, 7, 11),
                },
                7, 5
            );
        }

        [Fact]
        public void ReturnValues()
        {
            DoCompilerTest(
                "return a, b",
                new LuaValue[] { "a", "b" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(GETTABUP, 1, 0, 1 | KFlag),
                    Build2(RETURN, 0, 3),
                },
                0, 2
            );
        }

        [Fact]
        public void ReturnNothing()
        {
            DoCompilerTest(
                "return",
                new LuaValue[] { },
                new [] {
                    Build2(RETURN, 0, 1),
                },
                0, 0
            );
        }

        [Fact]
        public void ReturnVararg()
        {
            DoCompilerTest(
                "return 1, ...",
                new LuaValue[] { 1 },
                new [] {
                    Build2x(LOADK, 0, 0),
                    Build2(VARARG, 1, 0),
                    Build2(RETURN, 0, 0),
                },
                0, 2
            );
        }

        [Fact]
        public void IfElseifElse()
        {
            DoCompilerTest(
                "if x then a() elseif y then b() elseif z then c() else d() end",
                new LuaValue[] { "x", "a", "y", "b", "z", "c", "d" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(TEST, 0, 0, 0),
                    Build2sx(JMP, 0, 3),
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(CALL, 0, 1, 1),
                    Build2sx(JMP, 0, 14),
                    Build3(GETTABUP, 0, 0, 2 | KFlag),
                    Build3(TEST, 0, 0, 0),
                    Build2sx(JMP, 0, 3),
                    Build3(GETTABUP, 0, 0, 3 | KFlag),
                    Build3(CALL, 0, 1, 1),
                    Build2sx(JMP, 0, 8),
                    Build3(GETTABUP, 0, 0, 4 | KFlag),
                    Build3(TEST, 0, 0, 0),
                    Build2sx(JMP, 0, 3),
                    Build3(GETTABUP, 0, 0, 5 | KFlag),
                    Build3(CALL, 0, 1, 1),
                    Build2sx(JMP, 0, 2),
                    Build3(GETTABUP, 0, 0, 6 | KFlag),
                    Build3(CALL, 0, 1, 1),
                },
                0, 1
            );
        }

        [Fact]
        public void While()
        {
            DoCompilerTest(
                "while x do a() end",
                new LuaValue[] { "x", "a" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(TEST, 0, 0, 0),
                    Build2sx(JMP, 0, 3),
                    Build3(GETTABUP, 0, 0, 1 | KFlag),
                    Build3(CALL, 0, 1, 1),
                    Build2sx(JMP, 0, -6),
                },
                0, 1
            );
        }

        [Fact]
        public void LogicalConstants1()
        {
            DoCompilerTest(
                "if true then x() end",
                new LuaValue[] { "x" },
                new [] {
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(CALL, 0, 1, 1),
                },
                0, 1
            );
        }

        [Fact]
        public void LogicalConstants2()
        {
            DoCompilerTest(
                "if false then x() end",
                new LuaValue[] { "x" },
                new [] {
                    Build3(LOADBOOL, 0, 0, 0),
                    Build3(TEST, 0, 0, 0),
                    Build2sx(JMP, 0, 2),
                    Build3(GETTABUP, 0, 0, 0 | KFlag),
                    Build3(CALL, 0, 1, 1),
                },
                0, 1
            );
        }

        [Fact]
        public void LogicalConstants3()
        {
            DoCompilerTest(
                "local a = true and 1",
                new LuaValue[] { "x" },
                new [] {
                    Build2x(LOADK, 0, 0),
                },
                1, 0
            );
        }
    }
}
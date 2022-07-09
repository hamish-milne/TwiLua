using System;
using Xunit;

namespace YANCL.Test
{
    public class VMTests
    {
        static void CheckBinary(int expected, int actual) {
            if (expected != actual) {
                throw new Exception($"Expected {Convert.ToString(expected, 2)} but got {Convert.ToString(actual, 2)}");
            }
        }

        [Fact]
        public void Instructions()
        {
            unchecked {
                CheckBinary(
                    0b000000011_100000001_00000100_001101,
                    Instruction.Build3(OpCode.ADD, 4, 3, 1 | Instruction.KFlag)
                );
                CheckBinary(
                    0b000000001_000000000_00000000_100110,
                    Instruction.Build2(OpCode.RETURN, 0, 1)
                );
                CheckBinary(
                    (int)0b100000000000000010_00000000_101000,
                    Instruction.Build2sx(OpCode.FORPREP, 0, 3)
                );
                CheckBinary(
                    0b011111111111111011_00000000_100111,
                    Instruction.Build2sx(OpCode.FORLOOP, 0, -4)
                );
            }
        }

        void BinaryOperation(OpCode op, LuaValue a, LuaValue b, LuaValue expected) {
            var function = new LuaFunction{
                code = new int[] {
                    Instruction.Build3(op, 1, 0, 0 | Instruction.KFlag),
                    Instruction.Build2(OpCode.RETURN, 1, 2),
                    Instruction.Build2(OpCode.RETURN, 0, 1),
                },
                constants = new LuaValue[] {
                    b,
                },
                upvalues = Array.Empty<UpValueInfo>(),
                prototypes = Array.Empty<LuaFunction>(),
                nParams = 1,
                nSlots = 1,
            };
            var state = new LuaThread(isMain: true, 3, 1);
            var closure = new LuaClosure(function, new []{new LuaUpValue()});
            var results = state.Execute(closure, a);
            Assert.Equal(new []{expected}, results);
        }

        [Fact]
        public void Add() {
            BinaryOperation(OpCode.ADD, 1, 2, 3);
        }

        [Fact]
        public void Sub() {
            BinaryOperation(OpCode.SUB, 3, 1, 2);
        }

        [Fact]
        public void Mul() {
            BinaryOperation(OpCode.MUL, 3, 2, 6);
        }

        [Fact]
        public void Div() {
            BinaryOperation(OpCode.DIV, 6, 2, 3);
        }

        [Fact]
        public void Mod() {
            BinaryOperation(OpCode.MOD, 6, 2, 0);
        }

        [Fact]
        public void Pow() {
            BinaryOperation(OpCode.POW, 2, 3, 8);
        }

        [Fact]
        public void Concat() {
            var function = new LuaFunction{
                code = new int[] {
                    Instruction.Build3(OpCode.CONCAT, 0, 0, 2),
                    Instruction.Build2(OpCode.RETURN, 0, 2),
                    Instruction.Build2(OpCode.RETURN, 0, 1),
                },
                constants = new LuaValue[] {},
                upvalues = Array.Empty<UpValueInfo>(),
                prototypes = Array.Empty<LuaFunction>(),
                nParams = 3,
                nSlots = 3,
            };
            var state = new LuaThread(isMain: true, 4, 1);
            var closure = new LuaClosure(function, new []{new LuaUpValue()});
            var results = state.Execute(closure, "a", "b", "c");
            Assert.Equal(new []{new LuaValue("abc")}, results);
        }

        [Fact]
        public void CallCFunction() {
            
            LuaCFunction cf = s => {
                Assert.Equal("foo", s.String());
                return s.Return("bar");
            };
            var function = new LuaFunction{
                code = new int[] {
                    Instruction.Build2x(OpCode.LOADK, 0, 0),
                    Instruction.Build2x(OpCode.LOADK, 1, 1),
                    Instruction.Build3(OpCode.CALL, 0, 2, 2),
                    Instruction.Build2(OpCode.RETURN, 0, 2),
                    Instruction.Build2(OpCode.RETURN, 0, 1),
                },
                constants = new LuaValue[] {cf, "foo"},
                upvalues = Array.Empty<UpValueInfo>(),
                prototypes = Array.Empty<LuaFunction>(),
                nParams = 0,
                nSlots = 2,
            };
            var state = new LuaThread(isMain: true, 3, 2);
            var closure = new LuaClosure(function, new []{new LuaUpValue()});
            var results = state.Execute(closure);
            Assert.Equal(new LuaValue[]{"bar"}, results);
        }
    }
}

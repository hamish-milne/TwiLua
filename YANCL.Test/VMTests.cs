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
                },
                constants = new LuaValue[] {
                    b,
                },
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = Array.Empty<LuaFunction>(),
                entry = 0,
                nParams = 1,
                nLocals = 1,
                nSlots = 0,
            };
            var state = new LuaState(3, 1);
            var results = state.Execute(function, a);
            Assert.Equal(new []{expected}, results);
        }

        [Fact]
        public void Add() {
            BinaryOperation(OpCode.ADD, new LuaValue(1), new LuaValue(2), new LuaValue(3));
        }

        [Fact]
        public void Sub() {
            BinaryOperation(OpCode.SUB, new LuaValue(3), new LuaValue(2), new LuaValue(1));
        }

        [Fact]
        public void Mul() {
            BinaryOperation(OpCode.MUL, new LuaValue(3), new LuaValue(2), new LuaValue(6));
        }

        [Fact]
        public void Div() {
            BinaryOperation(OpCode.DIV, new LuaValue(6), new LuaValue(2), new LuaValue(3));
        }

        [Fact]
        public void Mod() {
            BinaryOperation(OpCode.MOD, new LuaValue(6), new LuaValue(2), new LuaValue(0));
        }

        [Fact]
        public void Pow() {
            BinaryOperation(OpCode.POW, new LuaValue(2), new LuaValue(3), new LuaValue(8));
        }

        [Fact]
        public void Concat() {
            var function = new LuaFunction{
                code = new int[] {
                    Instruction.Build3(OpCode.CONCAT, 0, 0, 2),
                    Instruction.Build2(OpCode.RETURN, 0, 2),
                },
                constants = new LuaValue[] {},
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = Array.Empty<LuaFunction>(),
                entry = 0,
                nParams = 3,
                nLocals = 0,
                nSlots = 0,
            };
            var state = new LuaState(4, 1);
            var results = state.Execute(function, new LuaValue("a"), new LuaValue("b"), new LuaValue("c"));
            Assert.Equal(new []{new LuaValue("abc")}, results);
        }
    }
}

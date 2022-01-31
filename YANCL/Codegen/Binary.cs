using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {

        class TBinary : Operand
        {
            public readonly OpCode OpCode;
            public readonly int OpA, OpB;

            public TBinary(Compiler c, OpCode opCode, Operand opA, Operand? opB) {
                OpCode = opCode;
                OpA = opA.GetRK(c, ref stackSlots);
                OpB = opB?.GetRK(c, ref stackSlots) ?? 0;
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(OpCode, dst, OpA, OpB));
        }

        delegate double ArithmeticOp(double a, double b);
        private void Arithmetic(OpCode opcode, ArithmeticOp operation) {
            var opB = Pop();
            var opA = Pop();
            if (opB is TConstant cB &&
                opA is TConstant cA &&
                cB.Value.Type == LuaType.NUMBER &&
                cA.Value.Type == LuaType.NUMBER) {
                Push(new TConstant(operation(cA.Value.Number, cB.Value.Number)));
            } else {
                Push(new TBinary(this, opcode, opA, opB));
            }
        }


        public void Add() => Arithmetic(ADD, (a, b) => a + b);
        public void Sub() => Arithmetic(SUB, (a, b) => a - b);
        public void Mul() => Arithmetic(MUL, (a, b) => a * b);
        public void Div() => Arithmetic(DIV, (a, b) => a / b);
        public void Mod() => Arithmetic(MOD, (a, b) => a % b);
        public void Pow() => Arithmetic(POW, Math.Pow);

        delegate long IntegerOp(long a, long b);
        private void IArithmetic(OpCode opcode, IntegerOp operation) {
            var opB = Pop();
            var opA = Pop();
            if (opB is TConstant cB &&
                opA is TConstant cA &&
                cB.Value.Type == LuaType.NUMBER &&
                cA.Value.Type == LuaType.NUMBER &&
                cB.Value.Number % 1 == 0 &&
                cA.Value.Number % 1 == 0) {
                Push(new TConstant(operation((long)cA.Value.Number, (long)cB.Value.Number)));
            } else {
                Push(new TBinary(this, opcode, opA, opB));
            }
        }

        public void BAnd() => IArithmetic(BAND, (a, b) => a & b);
        public void BOr() => IArithmetic(BOR, (a, b) => a | b);
        public void BXor() => IArithmetic(BXOR, (a, b) => a ^ b);
        public void IDiv() => Arithmetic(IDIV, (a, b) => a / b);
        public void Shl() => IArithmetic(SHL, (a, b) => b < 64 && b > -64 ? a << (int)b : 0);
        public void Shr() => IArithmetic(SHR, (a, b) => b < 64 && b > -64 ? a >> (int)b : 0);

        // TODO: Consider fixing the unary+const case
        public void Value() {
            var op = Peek(0);
            if (op is TConstant || op is TLocal) {
                return;
            }
            Pop().Load(this, Top);
            Push(new TLocal(Top, isVar: false));
        }
    }
}
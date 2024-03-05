using System;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        class TBinary : OperandWithSlots
        {
            public OpCode OpCode  { get; private set; }
            public int OpA { get; private set; }
            public int OpB { get; private set; }

            public TBinary Init(Compiler c, OpCode opCode, Operand opA, Operand opB) {
                stackSlots = 0;
                OpCode = opCode;
                OpA = opA.GetRK(c, ref stackSlots);
                OpB = opB.GetRK(c, ref stackSlots);
                return this;
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(OpCode, dst, OpA, OpB));
        }

        private void Arithmetic(OpCode opcode) {
            var opB = Pop();
            var opA = Pop();
            if (opB is TConstant cB &&
                opA is TConstant cA &&
                cB.Value.TryGetNumber(out var b) &&
                cA.Value.TryGetNumber(out var a)) {
                Constant(opcode switch {
                    ADD => a + b,
                    SUB => a - b,
                    MUL => a * b,
                    DIV => a / b,
                    MOD => a % b,
                    POW => Math.Pow(a, b),
                    _ => throw new InvalidOperationException()
                });
            } else {
                Push<TBinary>().Init(this, opcode, opA, opB);
            }
            Release(opB);
            Release(opA);
        }

        public void Add() => Arithmetic(ADD);
        public void Sub() => Arithmetic(SUB);
        public void Mul() => Arithmetic(MUL);
        public void Div() => Arithmetic(DIV);
        public void Mod() => Arithmetic(MOD);
        public void Pow() => Arithmetic(POW);

        private void IArithmetic(OpCode opcode) {
            var opB = Pop();
            var opA = Pop();
            if (opB is TConstant cB &&
                opA is TConstant cA &&
                cB.Value.TryGetInteger(out var b) &&
                cA.Value.TryGetInteger(out var a)) {
                Constant(opcode switch {
                    BAND => a & b,
                    BOR => a | b,
                    BXOR => a ^ b,
                    SHL => b < 64 && b > -64 ? a << (int)b : 0,
                    SHR => b < 64 && b > -64 ? a >> (int)b : 0,
                    _ => throw new InvalidOperationException()
                });
            } else {
                Push<TBinary>().Init(this, opcode, opA, opB);
            }
            Release(opB);
            Release(opA);
        }

        public void BAnd() => IArithmetic(BAND);
        public void BOr() => IArithmetic(BOR);
        public void BXor() => IArithmetic(BXOR);
        public void IDiv() => Arithmetic(IDIV);
        public void Shl() => IArithmetic(SHL);
        public void Shr() => IArithmetic(SHR);
    }
}
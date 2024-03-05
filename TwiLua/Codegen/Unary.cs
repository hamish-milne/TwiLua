using System;
using System.Collections.Generic;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        class TUnary : OperandWithSlots
        {
            public OpCode OpCode { get; private set; }
            public int OpA { get; private set; }

            public TUnary Init(Compiler c, OpCode opcode, Operand operand) {
                stackSlots = 0;
                OpCode = opcode;
                OpA = operand.GetR(c, ref stackSlots);
                return this;
            }
            public override void Load(Compiler c, int dst) => c.Emit(Build2(OpCode, dst, OpA));
        }

        public void Unm() {
            var op = Pop();
            if (op is TConstant c && c.Value.TryGetNumber(out var n)) {
                Constant(-n);
            } else {
                Push<TUnary>().Init(this, UNM, op);
            }
            Release(op);
        }

        public void Not() {
            if (Peek(0) is TCondition cond) {
                cond.DoForceBool();
                cond.Invert(this);
                return;
            }
            if (Peek(0) is Logical logical)
            {
                logical.Invert();
                var last = logical.Last;
                Push(last);
                Not();
                logical.Last = Pop();
                logical.Update();
                Top += logical.StackSlots;
                return;
            }
            var op = Pop();
            if (op is TConstant c) {
                Constant(!c.Value.Boolean);
            } else {
                Push<TUnary>().Init(this, NOT, op);
            }
            Release(op);
        }
        
        public void BNot() {
            var op = Pop();
            if (op is TConstant c && c.Value.TryGetInteger(out var i)) {
                Constant(~i);
            } else {
                Push<TUnary>().Init(this, BNOT, op);
            }
            Release(op);
        }

        public void Len() => Push(Acquire<TUnary>().Init(this, LEN, PopAndRelease()));
    }
}

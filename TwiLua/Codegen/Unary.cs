using System;
using System.Collections.Generic;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    public partial class Compiler
    {
        class TUnary : Operand
        {
            public readonly OpCode OpCode;
            public readonly int OpA;
            public TUnary(Compiler c, OpCode opcode, Operand operand) {
                OpCode = opcode;
                OpA = operand.GetR(c, ref stackSlots);
            }
            public override void Load(Compiler c, int dst) => c.Emit(Build2(OpCode, dst, OpA));
        }

        public void Unm() {
            var op = Pop();
            if (op is TConstant c && c.Value.Type == LuaType.NUMBER) {
                Push(new TConstant(-c.Value.Number));
            } else {
                Push(new TUnary(this, UNM, op));
            }
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
            if (op is TConstant c && c.Value.Type switch {
                LuaType.NIL => true,
                LuaType.BOOLEAN => true,
                LuaType.STRING => true,
                LuaType.NUMBER => true,
                _ => false
            }) {
                Push(new TConstant(!c.Value.Boolean));
            } else {
                Push(new TUnary(this, NOT, op));
            }
        }
        
        public void BNot() {
            var op = Pop();
            if (op is TConstant c && c.Value.Type == LuaType.NUMBER && c.Value.Number % 1 == 0) {
                Push(new TConstant(~(long)c.Value.Number));
            } else {
                Push(new TUnary(this, BNOT, op));
            }
        }

        public void Len() => Push(new TUnary(this, LEN, Pop()));
    }
}

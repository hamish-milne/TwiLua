using System;
using System.Collections.Generic;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    public partial class Compiler
    {
        class TConcat : Operand
        {
            public readonly int Start;
            public TConcat(int start) {
                Start = start;
                stackSlots = 1;
            }
            public void Add() => stackSlots++;

            public override void Load(Compiler c, int dst) => c.Emit(Build3(CONCAT, dst, Start, Start + stackSlots - 1));
        }

        public void ConcatArg() {
            if (Peek(0) is TConcat) {
                return;
            }
            Argument();
            if (operands.Count > 0 && Peek(0) is TConcat concat && Top - 1 == concat.Start + concat.StackSlots) {
                concat.Add();
            } else {
                Push(new TConcat(Top - 1));
            }
        }

        public void Concat() {
            ConcatArg();
        }
    }
}

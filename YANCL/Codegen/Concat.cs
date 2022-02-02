using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        class TConcat : Operand
        {
            public readonly int Start;
            public TConcat(int start) => Start = start;
            public void Add(Compiler c, Operand op) {
                op.Load(c, c.PushS());
                stackSlots++;
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(CONCAT, dst, Start, Start + stackSlots - 1));
        }

        public void Concat() {
            var opB = Pop();
            if (Peek(0) is TConcat concat) {
                concat.Add(this, opB);
            } else {
                var opA = Pop();
                var concat1 = new TConcat(Top);
                Push(concat1);
                concat1.Add(this, opA);
                concat1.Add(this, opB);
            }
        }
    }
}

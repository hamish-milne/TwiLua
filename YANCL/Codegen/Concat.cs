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
            public void Add(Compiler c, Operand op) {
                op.Load(c, c.PushS());
                stackSlots++;
            }

            public override void Load(Compiler c, int dst) => c.Emit(Build3(CONCAT, dst, c.Top, c.Top + stackSlots - 1));
        }

        public void Concat() {
            var opB = Pop();
            if (Peek(0) is TConcat concat) {
                concat.Add(this, opB);
            } else {
                var concat1 = new TConcat();
                var opA = Pop();
                concat1.Add(this, opA);
                concat1.Add(this, opB);
                Push(concat1);
            }
        }
    }
}

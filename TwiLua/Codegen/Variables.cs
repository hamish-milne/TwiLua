using System;
using System.Collections.Generic;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    public partial class Compiler
    {
        public void InitLocals(int count, int arguments)
        {
            if (count <= 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (arguments > 0 && Peek(0) is Varadic v) {
                Pop();
                var limit = Math.Max(count - arguments + 1, 0);
                v.LoadVaradic(this, limit + 1);
                Top += limit;
                SetMaxStack();
            } else {
                if (arguments > 0) {
                    Argument();
                }
                if (arguments < count) {
                    Emit(Build2(LOADNIL, Top, count - arguments - 1));
                    Top += count - arguments;
                    SetMaxStack();
                }
            }
            // Drop extra arguments
            if (arguments > count) {
                Top -= arguments - count;
            }
        }

        public void Assign(int arguments, int targets)
        {
            if (arguments <= 0 || targets <= 0) {
                throw new ArgumentOutOfRangeException();
            }
            var lastArg = Pop();
            if (lastArg is Varadic v) {
                var limit = Math.Max(targets - arguments + 1, 0);
                v.LoadVaradic(this, limit + 1);
                Top += limit;
                SetMaxStack();
            } else if (arguments == targets) {
                // Optimized assignment of a single value
                var src = lastArg;
                var dst = Peek(0); // Peek to keep dst's dependencies
                if (dst is TLocal l) {
                    src.Load(this, l.Index);
                } else {
                    var tmpSlots = 0;
                    dst.Store(this, src.GetRK(this, ref tmpSlots));
                    Top -= tmpSlots;
                }
                Pop();
                targets--;
                arguments--;
            } else {
                lastArg.Load(this, PushS());
                if (arguments < targets) {
                    Emit(Build2(LOADNIL, Top, targets - arguments - 1));
                    Top += targets - arguments;
                    SetMaxStack();
                }
            }
            // Drop extra arguments
            if (arguments > targets) {
                Top -= arguments - targets;
            }
            while (targets > 0) {
                targets--;
                Pop().Store(this, --Top);
            }
        }

    }
}

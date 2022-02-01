using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public class Label
    {
        internal int Location = -1;
        internal readonly List<int> References = new List<int>();
        internal bool Used => References.Count > 0;
    }

    public partial class Compiler
    {
        public Label Label() => new Label();

        private void MarkAt(Label label, int dst) {
            if (label.Location < 0) {
                label.Location = dst;
                foreach (var refIdx in label.References) {
                    code[refIdx] = Build2sx(JMP, 0, label.Location - refIdx - 1);
                }
            } else {
                throw new InvalidOperationException("Label already marked");
            }
        }

        public void Mark(Label label) => MarkAt(label, code.Count);

        public void Jump(Label label) {
            if (label.Location < 0) {
                label.References.Add(code.Count);
                Emit(0);
            } else {
                Emit(Build2sx(JMP, 0, label.Location - code.Count - 1));
            }
        }

        private bool IsConstantTrue(Operand op) => op is TConstant c && (c.Value.Type switch {
            LuaType.NIL => false,
            LuaType.BOOLEAN => c.Value.Boolean,
            LuaType.STRING => true,
            LuaType.NUMBER => true,
            _ => false
        });

        public void JumpIf(bool test, Label label) {
            if (IsConstantTrue(Peek(0)) && !test) {
                Pop();
                return;
            }
            Test();
            if (Pop() is TCondition cond) {
                if (!test) {
                    cond.Invert(this);
                }
                label.References.Add(cond.Jump);
            } else {
                throw new InvalidOperationException("Expected condition");
            }
        }
    }
}

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
                    code[refIdx] = Build2sx(GetOpCode(code[refIdx]), GetA(code[refIdx]), label.Location - refIdx - 1);
                }
            } else {
                throw new InvalidOperationException("Label already marked");
            }
        }

        public void Mark(Label label) => MarkAt(label, code.Count);

        private void JumpAt(Label label, int inst) {
            if (label.Location < 0) {
                label.References.Add(inst);
            } else {
                code[inst] = Build2sx(GetOpCode(code[inst]), GetA(code[inst]), label.Location - inst - 1);
            }
        }

        public void Jump(Label label) {
            Emit(Build2sx(JMP, 0, 0));
            JumpAt(label, code.Count - 1);
        }

        private bool IsConstantTrue(Operand op) => op is TConstant c && (c.Value.Type switch {
            LuaType.NIL => false,
            LuaType.BOOLEAN => c.Value.Boolean,
            LuaType.STRING => true,
            LuaType.NUMBER => true,
            _ => false
        });

        public void JumpIfFalse(Label label) {
            if (IsConstantTrue(Peek(0))) {
                Pop();
                return;
            }
            Test();
            var op = Pop();
            if (op is Logical logical) {
                Mark(logical.True);
                label.References.AddRange(logical.False.References);
                if (logical.DoInvert) {
                    label.References.AddRange(logical.Value.References);
                } else {
                    Mark(logical.Value);
                }
                op = logical.Last;
            }
            if (op is TCondition cond) {
                cond.Invert(this);
                JumpAt(label, cond.Jump);
            } else {
                throw new InvalidOperationException("Expected condition");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        abstract class TCondition : Operand
        {
            public int Jump, Test;
            public abstract void Invert(Compiler c);

            public TCondition(int jump, int test) {
                Jump = jump;
                Test = test;
            }

            public override void Load(Compiler c, int dst)
            {
                Invert(c);
                c.Emit(Build3(LOADBOOL, dst, 0, 1));
                c.code[Jump] = Build2sx(JMP, 0, c.code.Count - Jump - 1);
                c.Emit(Build3(LOADBOOL, dst, 1, 0));
            }
        }

        class TTest : TCondition
        {
            public TTest(int jump, int test) : base(jump, test)
            {
            }

            public override void Invert(Compiler c)
            {
                var testInst = c.code[Test];
                c.code[Test] = Build3(GetOpCode(testInst), GetA(testInst), GetB(testInst), GetC(testInst) == 0 ? 1 : 0);
            }
        }

        class Logical : Operand
        {
            public readonly Label Label = new Label();
            public readonly List<int> Outputs = new List<int>();
            public Operand Last;

            public void Update() => stackSlots = Last.StackSlots;

            public override void Load(Compiler c, int dst)
            {
                Last.Load(c, dst);
                c.Mark(Label);
                foreach (var a in Outputs) {
                    var testInst = c.code[a];
                    if (dst != GetA(testInst)) {
                        c.code[a] = Build3(TESTSET, dst, GetA(testInst), GetC(testInst));
                    }
                }
            }
        }

        public void Test(bool keepConstantTrue = false)
        {
            var op = Peek(0);
            if (op is TCondition) {
                return;
            }
            // This is a bit of a hack; not sure how else to get the correct behaviour.
            if (keepConstantTrue && IsConstantTrue(op)) {
                return;
            }
            if (op is Logical l) {
                Pop();
                Push(l.Last);
                Test();
                l.Last = Pop();
                l.Update();
                Push(l);
                return;
            }
            var slots = 0;
            var a = Pop().GetR(this, ref slots);
            Top -= slots;
            var test = code.Count;
            Emit(Build3(TEST, a, 0, 1));
            var jump = code.Count;
            Emit(0);
            Push(new TTest(jump, test));
        }

        private void LogicalOp(bool doInvert)
        {
            var opB = Pop();

            // Constant folding for 'and':
            if (doInvert && IsConstantTrue(Peek(0))) {
                Pop();
                Push(opB);
                Top += opB.StackSlots;
                return;
            }

            var opA = Pop();
            var newOp = new Logical();

            if (opA is Logical logical) {
                if (logical.Last is TCondition lastCond) {
                    MarkAt(logical.Label, lastCond.Jump + 1);
                    opA = logical.Last;
                } else {
                    throw new InvalidOperationException();
                }
            }
            if (opA is TCondition cond) {
                if (doInvert) {
                    cond.Invert(this);
                }
                newOp.Label.References.Add(cond.Jump);
                newOp.Outputs.Add(cond.Test);
            } else {
                throw new InvalidOperationException();
            }

            if (opB is Logical logical1) {
                newOp.Label.References.AddRange(logical1.Label.References);
                newOp.Outputs.AddRange(logical1.Outputs);
                newOp.Last = logical1.Last;
            } else {
                newOp.Last = opB;
            }
            newOp.Update();
            Top += newOp.StackSlots;
            Push(newOp);
        }

        public void And() => LogicalOp(doInvert: true);
        public void Or() => LogicalOp(doInvert: false);
    }
}

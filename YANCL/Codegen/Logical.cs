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

            public override void Load(Compiler c, int dst)
            {
                c.Emit(Build3(LOADBOOL, dst, 0, 1));
                c.code[Jump] = Build2sx(JMP, 0, c.code.Count - Jump - 1);
                c.Emit(Build3(LOADBOOL, dst, 1, 0));
            }
        }

        class TTest : TCondition
        {
            public override void Invert(Compiler c)
            {
                var testInst = c.code[Test];
                c.code[Test] = Build3(GetOpCode(testInst), GetA(testInst), GetB(testInst), GetC(testInst) == 0 ? 1 : 0);
            }
        }

        class Logical : Operand
        {
            public readonly Label Value = new Label();
            public readonly Label True = new Label();
            public readonly Label False = new Label();
            public readonly bool DoInvert;
            public readonly List<int> Outputs = new List<int>();
            public Operand Last;

            public Logical(bool doInvert) => DoInvert = doInvert;

            public void Update() => stackSlots = Last.StackSlots;

            public void Add(Logical other) {
                Value.References.AddRange(other.Value.References);
                True.References.AddRange(other.True.References);
                False.References.AddRange(other.False.References);
                Outputs.AddRange(other.Outputs);
            }

            public override void Load(Compiler c, int dst)
            {
                if (Last is TCondition cond) {
                    True.References.Add(cond.Jump);
                } else {
                    Last.Load(c, dst);
                    if (True.Used || False.Used) {
                        c.Emit(Build2sx(JMP, 0, 2));
                    }
                }
                if (True.Used || False.Used) {
                    c.Mark(False);
                    c.Emit(Build3(LOADBOOL, dst, 0, 1));
                    c.Mark(True);
                    c.Emit(Build3(LOADBOOL, dst, 1, 0));
                }
                c.Mark(Value);
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
            Push(new TTest {
                Jump = jump,
                Test = test
            });
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
            var newOp = new Logical(doInvert);

            if (opA is Logical logical) {
                if (logical.DoInvert == doInvert) {
                    newOp.Add(logical);
                    opA = logical.Last;
                } else if (logical.Last is TCondition lastCond) {
                    MarkAt(logical.Value, lastCond.Jump + 1);
                    MarkAt(logical.True, lastCond.Jump + 1);
                    MarkAt(logical.False, lastCond.Jump + 1);
                    opA = logical.Last;
                } else {
                    throw new InvalidOperationException();
                }
            }
            if (opA is TTest cond) {
                if (doInvert) {
                    cond.Invert(this);
                }
                newOp.Value.References.Add(cond.Jump);
                newOp.Outputs.Add(cond.Test);
            } else if (opA is TComparison comp) {
                if (doInvert) {
                    comp.Invert(this);
                }
                (doInvert ? newOp.False : newOp.True).References.Add(comp.Jump);
            } else {
                throw new InvalidOperationException();
            }

            if (opB is Logical logical1) {
                newOp.Add(logical1);
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

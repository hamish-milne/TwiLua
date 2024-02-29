using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        class TComparison : TCondition
        {
            public override bool ForceBool => true;
            public TComparison(Compiler c, OpCode opCode, bool invert, bool swap, Operand opA, Operand opB) {
                var a = opA.GetRK(c, ref stackSlots);
                var b = opB.GetRK(c, ref stackSlots);
                Test = c.code.Count;
                c.Emit(Build3(opCode, invert ? 0 : 1, swap ? b : a, swap ? a : b));
                Jump = c.code.Count;
                c.Emit(Build2sx(JMP, 0, 0));
            }

            public override void Invert(Compiler c)
            {
                var testInst = c.code[Test];
                c.code[Test] = Build3(GetOpCode(testInst), GetA(testInst) == 0 ? 1 : 0, GetB(testInst), GetC(testInst));
            }
        }

        private void Compare(OpCode opCode, bool invert, bool swap) {
            var opB = Pop();
            var opA = Pop();
            Push(new TComparison(this, opCode, invert, swap, opA, opB));
        }

        public void Eq() => Compare(EQ, invert: false, swap: false);
        public void Ne() => Compare(EQ, invert: true, swap: false);
        public void Lt() => Compare(LT, invert: false, swap: false);
        public void Le() => Compare(LE, invert: false, swap: false);
        public void Gt() => Compare(LT, invert: false, swap: true);
        public void Ge() => Compare(LE, invert: false, swap: true);
    }
}

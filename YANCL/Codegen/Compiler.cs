using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    public partial class Compiler
    {
        public Compiler() { }
        private Compiler(Compiler parent, int prototypeIdx) {
            this.parent = parent;
            this.prototypeIdx = prototypeIdx;
        }

        abstract class Operand
        {
            protected int stackSlots;
            public int StackSlots => stackSlots;

            public virtual int GetR(Compiler c, ref int tmpSlots) {
                tmpSlots++;
                var idx = c.PushS();
                Load(c, idx);
                return idx;
            }
            public virtual int GetRK(Compiler c, ref int tmpSlots) => GetR(c, ref tmpSlots);
            public abstract void Load(Compiler c, int dst);
            public virtual void Store(Compiler c, int src) => throw new NotSupportedException();
        }

        class TConstant : Operand
        {
            public readonly LuaValue Value;
            public TConstant(LuaValue value) => Value = value;
            public override int GetRK(Compiler c, ref int tmpSlots)  => c.K(Value) | KFlag;
            public override void Load(Compiler c, int dst) => c.Emit(Value.Type switch {
                LuaType.NIL => Build2(LOADNIL, dst, 0),
                LuaType.BOOLEAN => Build3(LOADBOOL, dst, Value.Boolean ? 1 : 0, 0),
                _ => Build2x(LOADK, dst, c.K(Value))
            });
        }

        private readonly List<Operand> operands = new List<Operand>();
        private readonly List<int> code = new List<int>();
        private readonly List<LuaValue> constants = new List<LuaValue>();
        int Top;
        int maxStack;

        public bool IsVaradic { get; set; }

        private int PushS() {
            var idx = Top++;
            SetMaxStack();
            return idx;
        }

        private void SetMaxStack() {
            if (maxStack < Top) maxStack = Top;
        }

        private Operand Peek(int idx) => operands[operands.Count - idx - 1];

        private void Push(Operand operand) {
            operands.Add(operand);
        }

        private Operand Pop() {
            var operand = Peek(0);
            operands.RemoveAt(operands.Count - 1);
            Top -= operand.StackSlots;
            return operand;
        }
        
        int K(LuaValue value) {
            var idx = constants.IndexOf(value);
            if (idx == -1) {
                idx = constants.Count;
                constants.Add(value);
            }
            return idx;
        }

        void Emit(int instruction) {
            if (code.Count > 0) {
                // Combine consecutive LOADNILs
                var prev = code[code.Count - 1];
                if (GetOpCode(instruction) == LOADNIL &&
                    GetOpCode(prev) == LOADNIL &&
                    GetA(prev)+GetB(prev)+1 == GetA(instruction)) {
                    code[code.Count - 1] = Build2(LOADNIL, GetA(prev), GetB(prev) + GetB(instruction) + 1);
                    return;
                }
            }
            code.Add(instruction);
        }

        public void Constant(LuaValue value) => Push(new TConstant(value));

        public LuaFunction MakeFunction()
        {
            return new LuaFunction {
                code = code.ToArray(),
                constants = constants.ToArray(),
                upvalues = upValues.ToArray(),
                prototypes = closures.ToArray()!,
                locals = locals.ToArray(),
                nParams = 0,
                nSlots = maxStack - Top,
            };
        }
    }
}
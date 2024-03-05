using System;
using System.Collections.Generic;
using System.Linq;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    internal sealed partial class Compiler
    {
        private readonly Dictionary<Type, List<Operand>> pools;

        private T Acquire<T>() where T : Operand, new() {
            pools.TryGetValue(typeof(T), out var pool);
            if (pool?.Count > 0) {
                var operand = (T)pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                return operand;
            }
            return new T();
        }

        private void Release(Operand operand) {
            if (!pools.TryGetValue(operand.GetType(), out var pool)) {
                pool = new List<Operand>();
                pools[operand.GetType()] = pool;
            }
            pool.Add(operand);
        }

        public Compiler(string chunkName) {
            this.chunkName = chunkName;
            this.pools = new();
        }
        private Compiler(Compiler parent, int prototypeIdx) {
            this.parent = parent;
            this.prototypeIdx = prototypeIdx;
            this.chunkName = parent.chunkName;
            this.pools = parent.pools;
        }

        abstract class Operand
        {
            public virtual int StackSlots => 0;

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

        abstract class OperandWithSlots : Operand
        {
            protected int stackSlots;
            public override int StackSlots => stackSlots;
        }

        class TConstant : Operand
        {
            public LuaValue Value { get; private set; }
            public TConstant Init(LuaValue value) {
                Value = value;
                return this;
            }
            public override int GetRK(Compiler c, ref int tmpSlots)  => c.K(Value) | KFlag;
            public override void Load(Compiler c, int dst) => c.Emit(Value.Type switch {
                TypeTag.Nil => Build2(LOADNIL, dst, 0),
                TypeTag.True or TypeTag.False => Build3(LOADBOOL, dst, Value.Boolean ? 1 : 0, 0),
                _ => Build2x(LOADK, dst, c.K(Value))
            });
        }

        private T Push<T>() where T : Operand, new() {
            var operand = Acquire<T>();
            Push(operand);
            return operand;
        }

        public void Constant(LuaValue value) {
            Push<TConstant>().Init(value);
        }

        private readonly string chunkName;
        private readonly List<Operand> operands = new();
        private readonly List<int> code = new();
        private readonly List<Location> locations = new();
        private readonly List<LuaValue> constants = new();
        int Top;
        int maxStack;

        public bool IsVaradic { get; set; }
        public Location Location { get; set; }

        internal void AssertStatementEnd() {
            // if (Top != locals.Count) {
            //     throw new InvalidOperationException("Stack not empty");
            // }
        }

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

        private Operand PopAndRelease() {
            var operand = Pop();
            Release(operand);
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
                var prev = code[code.Count - 1];

                // Combine consecutive LOADNILs
                if (GetOpCode(instruction) == LOADNIL &&
                    GetOpCode(prev) == LOADNIL &&
                    GetA(prev)+GetB(prev)+1 == GetA(instruction)) {
                    code[code.Count - 1] = Build2(LOADNIL, GetA(prev), GetB(prev) + GetB(instruction) + 1);
                    return;
                }
            }
            code.Add(instruction);
            locations.Add(Location);
        }

        public LuaFunction MakeFunction()
        {
            return new LuaFunction {
                code = code.ToArray(),
                constants = constants.ToArray(),
                upvalues = upValues.ToArray(),
                prototypes = closures.ToArray()!,
                locations = locations.ToArray(),
                chunkName = chunkName,
                locals = locals.ToArray(),
                nParams = locals.Count(l => l.Start == 0),
                nSlots = maxStack - Top,
            };
        }
    }
}
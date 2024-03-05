using System;
using static TwiLua.Instruction;
using static TwiLua.OpCode;

namespace TwiLua
{
    partial class Compiler
    {
        abstract class Varadic : Operand
        {
            public abstract void LoadVaradic(Compiler c, int dst);
        }

        class TCall : Varadic
        {
            public int Func { get; private set; }
            public int Args { get; private set; }
            public bool IsVaradic { get; private set; }
            public int B => IsVaradic ? 0 : (Args+1);

            private int stackSlots;
            public override int StackSlots => stackSlots;

            public TCall Init(Compiler c, int args, bool isVaradic) {
                Func = c.Top - args - 1;
                Args = args;
                IsVaradic = isVaradic;
                stackSlots = args + 1;
                return this;
            }

            public override int GetR(Compiler c, ref int tmpSlots) {
                if (Func != c.Top) {
                    throw new InvalidOperationException();
                }
                c.Emit(Build3(CALL, Func, B, 2));
                tmpSlots++;
                return c.PushS();
            }

            public override void Load(Compiler c, int dst) {
                var tmpSlots = 0;
                var r = GetR(c, ref tmpSlots);
                if (dst != r) c.Emit(Build2(MOVE, dst, r));
                c.Top -= tmpSlots;
            }

            public override void LoadVaradic(Compiler c, int limit) => c.Emit(Build3(CALL, Func, B, limit));
        }

        class Varargs : Varadic
        {
            public static Varargs Instance = new();
            private Varargs() { }
            public override void Load(Compiler c, int dst) => c.Emit(Build2(VARARG, dst, 2));
            public override void LoadVaradic(Compiler c, int limit) => c.Emit(Build2(VARARG, c.Top, limit)); 
        }

        
        public void Vararg() => Push(Varargs.Instance);

        public void Argument() {
            PopAndRelease().Load(this, Top);
            PushS();
        }

        public void Callee() => Argument();

        private int Dispatch(int arguments, int limit) {
            bool hasDispatch = false;
            if (arguments > 0) {
                if (Peek(0) is Varadic v) {
                    PopAndRelease();
                    v.LoadVaradic(this, limit);
                    PushS();
                    hasDispatch = true;
                } else {
                    Argument();
                }
            }
            return hasDispatch ? 0 : arguments+1;
        }

        public void Call(int prefix, int argCount) {
            var operand = Acquire<TCall>().Init(this, prefix + argCount, isVaradic: Dispatch(argCount, 0) == 0);
            Push(operand);
        }

        public void Discard() {
            var callOp = PopAndRelease();
            if (callOp is TCall call) {
                Emit(Build3(CALL, call.Func, call.B, 1));
            } else {
                throw new InvalidOperationException("Expected call");
            }
        }

        public void Return(int argCount) {
            if (argCount == 1 && Peek(0) is TLocal local) {
                Emit(Build2(RETURN, local.Index, 2));
                PopAndRelease();
            } else if (argCount == 1 && Peek(0) is TCall call) {
                Emit(Build3(TAILCALL, call.Func, call.B, 0));
                Emit(Build2(RETURN, call.Func, 0));
                PopAndRelease();
            } else {
                var b = Dispatch(argCount, 0);
                Emit(Build2(RETURN, argCount == 0 ? 0 : (Top-argCount), b));
                Top -= argCount;
            }
        }

        public void Self() {
            var indexer = PopAndRelease();
            var table = PopAndRelease();
            var slots = 0;
            var b = table.GetR(this, ref slots);
            var c = indexer.GetRK(this, ref slots);
            Top -= slots;
            var a = Top;
            Top += 2;
            SetMaxStack();
            Emit(Build3(SELF, a, b, c));
        }
    }
}
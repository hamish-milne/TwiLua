using System.Collections.Generic;
using System;
using static YANCL.Instruction;
using System.Runtime.CompilerServices;

namespace YANCL
{

    public readonly struct UpValueInfo
    {
        public readonly string Name;
        public readonly bool InStack;
        public readonly int Index;

        public UpValueInfo(string name, bool inStack, int index)
        {
            Name = name;
            InStack = inStack;
            Index = index;
        }

        public override string ToString() => $"{Name} {(InStack ? "in stack" : "in upvalue")} {Index}";
    }

    public readonly struct LocalVarInfo
    {
        public readonly string Name;
        public readonly int Start;
        public readonly int End;

        public LocalVarInfo(string name, int start, int end)
        {
            Name = name;
            Start = start;
            End = end;
        }

        public override string ToString() => $"{Name} {Start} {End}";
    }

    public sealed class LuaFunction
    {
        public int[] code;
        public LuaValue[] constants;
        public UpValueInfo[] upvalues;
        public LuaFunction[] prototypes;
        public LocalVarInfo[] locals;
        public LuaFunction? parent;
        public int nParams;
        public int nSlots;
        public bool IsVaradic;
    }

    public sealed class LuaUpValue
    {
        public LuaValue Value;
        public int Index = -1;

        public bool IsClosed {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Index < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Close(LuaValue value) {
            Value = value;
            Index = -1;
        }
    }

    public sealed class LuaClosure
    {
        public readonly LuaFunction Function;
        public readonly LuaUpValue[] UpValues;

        public LuaClosure(LuaFunction function, LuaUpValue[] upValues)
        {
            Function = function;
            UpValues = upValues;
        }
    }

    struct CallInfo {
        public int func;
        public int pc;
        public int top;
        public int baseR;
        public int nVarargs;
        public int expResults;
    }

    public class LuaCallState {
        private readonly LuaValue[] stack;

        internal LuaCallState(LuaValue[] stack)
        {
            this.stack = stack;
        }

        internal int Base;
        public int Count;
        public LuaValue this[int idx] {
            get => stack[Base + idx];
            set => stack[Base + idx] = value;
        }
        public double Number(int idx = 1) => this[idx].Number;
        public long Integer(int idx = 1) => (long)this[idx].Number;
        public string String(int idx = 1) => this[idx].ToString();
        public LuaTable Table(int idx = 1) => this[idx].Table!;
        public void Return(LuaValue v) {
            stack[Base] = v;
            Count = 1;
        }
    }

    public class LuaState {

        readonly LuaCallState callState;
        readonly LuaValue[] stack;
        readonly LuaUpValue?[] upValueStack;
        readonly CallInfo[] callStack;
        int callStackPtr;

        int func;
        int pc;
        int top;
        int baseR;
        int nVarargs;
        int expResults;

        int[] code;
        LuaUpValue[] parentUpValues;
        LuaValue[] constants;
        int nSlots;

        void SetFunc(int func) {
            this.func = func;
            var closure = stack[func].Function;
            if (closure != null) {
                code = closure.Function.code;
                if (code[code.Length - 1] != Build2(OpCode.RETURN, 0, 1)) {
                    throw new Exception("Invalid program");
                }
                constants = closure.Function.constants;
                parentUpValues = closure.UpValues;
                nSlots = closure.Function.nSlots;
            }
        }

        public LuaState(int stackSize, int callStackSize) {
            stack = new LuaValue[stackSize];
            upValueStack = new LuaUpValue[stackSize];
            callStack = new CallInfo[callStackSize];
            callState = new LuaCallState(stack);
        }

        public LuaValue[] Execute(LuaClosure closure, params LuaValue[] args) {
            var callee = baseR;
            stack[callee] = closure;
            Array.Copy(args, 0, stack, callee + 1, args.Length);
            PushCallInfo();
            Call(0, args.Length + 1, 0);
            var ci = callStackPtr;
            try {
                Execute(ci - 1);
            } catch {
                callStackPtr = ci;
                Return(func, 1);
                throw;
            }
            var resultsIdx = func;
            var nResults = top - resultsIdx;
            var results = new LuaValue[nResults];
            Array.Copy(stack, resultsIdx, results, 0, nResults);
            Array.Clear(stack, resultsIdx, nResults);
            return results; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushCallInfo() {
            callStack[callStackPtr++] = new CallInfo {
                func = func,
                pc = pc,
                baseR = baseR,
                top = top,
                nVarargs = nVarargs,
                expResults = expResults
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref CallInfo PopCallInfo() {
            return ref callStack[--callStackPtr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LuaValue R(int i) => ref stack[baseR + i];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LuaValue RK(int idx) {
            if ((idx & KFlag) != 0) {
                return ref constants[idx & 0xFF];
            } else {
                return ref stack[baseR + idx];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LuaValue UpVal(int i) {
            var upval = parentUpValues[i];
            if (upval.IsClosed) {
                return ref upval.Value;
            } else {
                return ref stack[upval.Index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Call(int callee, int nArgs, int nResults) {
            SetFunc(baseR + callee);
            expResults = nResults;
            baseR = func + 1;
            if (nArgs == 0) {
                nArgs = top - baseR;
            } else {
                nArgs--;
            }
            switch (stack[func].Type) {
            case LuaType.FUNCTION:
                var function = stack[func].Function!.Function;
                var nParams = function.nParams;

                // Populate unset fixed parameters with nil
                if (nArgs < nParams) {
                    nArgs = nParams;
                }

                // If the function is varadic and there are more arguments than expected,
                // copy the fixed arguments to the end of the stack and adjust the baseR
                nVarargs = nArgs - nParams;
                if (function.IsVaradic && nVarargs > 0) {
                    Array.Copy(stack, baseR, stack, baseR + nArgs, nParams);
                    baseR += nArgs;
                } else {
                    Array.Clear(stack, baseR + nParams, nVarargs);
                }

                pc = 0;
                break;
            case LuaType.CFUNCTION:
                callState.Base = func;
                callState.Count = nArgs;
                stack[func].CFunction!.Invoke(callState);
                nSlots = nArgs;
                Return(func, callState.Count + 1);
                break;
            default:
                throw new Exception("Tried to call a thing that isn't a function");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Return(int src, int nResults) {

            if (nResults == 0) {
                nResults = top - src;
            } else {
                nResults--;
            }
            if (expResults > 0 && expResults < nResults) {
                nResults = expResults;
            }

            var callInfo = PopCallInfo();

            var resultsIdx = func;
            Array.Copy(stack, src, stack, resultsIdx, nResults);
            var clearFrom = resultsIdx + nResults;
            var clearTo = func + nSlots;
            if (clearTo > clearFrom) {
                Array.Clear(stack, clearFrom, clearTo - clearFrom);
            }

            top = resultsIdx + nResults;
            pc = callInfo.pc;
            SetFunc(callInfo.func);
            baseR = callInfo.baseR;
            nVarargs = callInfo.nVarargs;
            expResults = callInfo.expResults;
        }

        void Close(int downTo) {
            for (int i = func + nSlots - 1; i >= downTo; i--) {
                var upval = upValueStack[i];
                if (upval != null) {
                    upval.Close(stack[i]);
                    upValueStack[i] = null;
                }
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Execute(int stopAt)
        {
            while (true) {
                var op = code[pc++];
                var opcode = GetOpCode(op);
                switch (opcode) {
                    case OpCode.MOVE:
                        R(GetA(op)) = R(GetB(op));
                        continue;
                    case OpCode.LOADK:
                        R(GetA(op)) = constants[GetBx(op)];
                        continue;
                    case OpCode.LOADBOOL:
                        R(GetA(op)) = (GetB(op) != 0);
                        if (GetC(op) != 0) {
                            pc++;
                        }
                        continue;
                    case OpCode.LOADNIL:
                        for (int a = GetA(op), b = a + GetB(op); a <= b; a++) {
                            stack[baseR + a] = LuaValue.Nil;
                        }
                        continue;
                    case OpCode.GETUPVAL:
                        R(GetA(op)) = UpVal(GetB(op));
                        continue;
                    case OpCode.GETTABUP:
                        R(GetA(op)) = UpVal(GetB(op))[RK(GetC(op))];
                        continue;
                    case OpCode.GETTABLE:
                        R(GetA(op)) = RK(GetB(op))[RK(GetC(op))];
                        continue;
                    case OpCode.SETTABUP:
                        UpVal(GetA(op))[RK(GetB(op))] = RK(GetC(op));
                        continue;
                    case OpCode.SETUPVAL:
                        UpVal(GetB(op)) = R(GetA(op));
                        continue;
                    case OpCode.SETTABLE:
                        RK(GetA(op))[RK(GetB(op))] = RK(GetC(op));
                        continue;
                    case OpCode.NEWTABLE:
                        R(GetA(op)) = new LuaTable();
                        continue;
                    case OpCode.SELF:
                        R(GetA(op)) = R(GetB(op))[RK(GetC(op))];
                        R(GetA(op) + 1) = R(GetB(op));
                        continue;
                    case OpCode.ADD:
                        R(GetA(op)) = (RK(GetB(op)).Number + RK(GetC(op)).Number);
                        continue;
                    case OpCode.SUB:
                        R(GetA(op)) = (RK(GetB(op)).Number - RK(GetC(op)).Number);
                        continue;
                    case OpCode.MUL:
                        R(GetA(op)) = (RK(GetB(op)).Number * RK(GetC(op)).Number);
                        continue;
                    case OpCode.MOD:
                        R(GetA(op)) = (RK(GetB(op)).Number % RK(GetC(op)).Number);
                        continue;
                    case OpCode.POW:
                        R(GetA(op)) = (Math.Pow(RK(GetB(op)).Number, RK(GetC(op)).Number));
                        continue;
                    case OpCode.DIV:
                        R(GetA(op)) = (RK(GetB(op)).Number / RK(GetC(op)).Number);
                        continue;
                    case OpCode.IDIV:
                        R(GetA(op)) = (Math.Floor(RK(GetB(op)).Number / RK(GetC(op)).Number));
                        continue;
                    case OpCode.BAND:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number & (long)RK(GetC(op)).Number);
                        continue;
                    case OpCode.BOR:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number | (long)RK(GetC(op)).Number);
                        continue;
                    case OpCode.BXOR:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number ^ (long)RK(GetC(op)).Number);
                        continue;
                    case OpCode.SHL:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number << (int)RK(GetC(op)).Number);
                        continue;
                    case OpCode.SHR:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number >> (int)RK(GetC(op)).Number);
                        continue;
                    case OpCode.UNM:
                        R(GetA(op)) = -R(GetB(op)).Number;
                        continue;
                    case OpCode.BNOT:
                        R(GetA(op)) = ~(long)RK(GetB(op)).Number;
                        continue;
                    case OpCode.NOT:
                        R(GetA(op)) = !R(GetB(op)).Boolean;
                        continue;
                    case OpCode.LEN:
                        R(GetA(op)) = R(GetB(op)).Length;
                        continue;
                    case OpCode.CONCAT: {
                        var b = GetB(op);
                        var sb = new string[GetC(op) - b + 1];
                        for (int i = 0; i < sb.Length; i++) {
                            sb[i] = R(b + i).String!;
                        }
                        R(GetA(op)) = string.Concat(sb);
                        continue;
                    }
                    case OpCode.JMP: {
                        var a = GetA(op);
                        if (a > 0) {
                            Close(baseR + a - 1);
                        }
                        pc += GetSbx(op);
                        continue;
                    }
                    case OpCode.EQ:
                        if ( (RK(GetB(op)) == RK(GetC(op))) != (GetA(op) != 0) ) {
                            pc++;
                        }
                        continue;
                    case OpCode.LT:
                        if ( (RK(GetB(op)).Number < RK(GetC(op)).Number) != (GetA(op) != 0) ) {
                            pc++;
                        }
                        continue;
                    case OpCode.LE:
                        if ( (RK(GetB(op)).Number <= RK(GetC(op)).Number) != (GetA(op) != 0) ) {
                            pc++;
                        }
                        continue;
                    case OpCode.TEST:
                        if ( R(GetA(op)).Boolean != (GetC(op) != 0) ) {
                            pc++;
                        }
                        continue;
                    case OpCode.TESTSET:
                        if ( R(GetB(op)).Boolean != (GetC(op) != 0) ) {
                            pc++;
                        } else {
                            R(GetA(op)) = R(GetB(op));
                        }
                        continue;
                    case OpCode.CALL:
                        PushCallInfo();
                        Call(GetA(op), GetB(op), GetC(op));
                        continue;
                    case OpCode.TAILCALL:
                        if ( R(GetA(op)).Type == LuaType.CFUNCTION ) {
                            goto case OpCode.CALL;
                        }
                        Close(baseR);
                        Call(GetA(op), GetB(op), GetC(op));
                        continue;
                    case OpCode.RETURN: {
                        Return(baseR + GetA(op), GetB(op));
                        if (callStackPtr == stopAt) {
                            return;
                        }
                        continue;
                    }
                    case OpCode.FORLOOP: {
                        var step = R(GetA(op) + 2).Number;
                        var limit = R(GetA(op) + 1).Number;
                        var i = R(GetA(op)).Number + step;
                        R(GetA(op)) = i;
                        if ( (step > 0 && i <= limit) || (step < 0 && i >= limit) ) {
                            pc += GetSbx(op);
                            R(GetA(op) + 3) = i;
                        }
                        continue;
                    }
                    case OpCode.FORPREP:
                        R(GetA(op)) = (R(GetA(op)).Number - R(GetA(op) + 2).Number);
                        pc += GetSbx(op);
                        continue;
                    case OpCode.SETLIST: {
                        var list = R(GetA(op)).Table!;
                        var block = GetC(op) - 1;
                        if (block < 0) {
                            block = code[pc++];
                        }
                        var n = GetB(op);
                        if ( n <= 0 ) {
                            n = top - GetA(op);
                        }
                        while ( (block*Lua.FieldsPerFlush + n) > list.Count ) {
                            list.Add(LuaValue.Nil);
                        }
                        for ( var i = 1; i <= n; i++ ) {
                            list[block*Lua.FieldsPerFlush + i] = R(GetA(op) + i);
                        }
                        continue;
                    }
                    case OpCode.CLOSURE: {
                        var proto = stack[func].Function!.Function.prototypes[GetBx(op)];
                        var upValues = new LuaUpValue[proto.upvalues.Length];
                        for (int i = 0; i < upValues.Length; i++) {
                            var upValInfo = proto.upvalues[i];
                            if (upValInfo.InStack) {
                                var idx = baseR + upValInfo.Index;
                                var upValObj = upValueStack[idx];
                                if (upValObj == null) {
                                    upValueStack[idx] = upValObj = new LuaUpValue { Index = idx };
                                }
                                upValues[i] = upValObj;
                            } else {
                                upValues[i] = parentUpValues[upValInfo.Index];
                            }
                        }
                        R(GetA(op)) = new LuaClosure(proto, upValues);
                        continue;
                    }
                    case OpCode.VARARG: {
                        var a = GetA(op);
                        var b = GetB(op);
                        if (b == 0) {
                            top = a + nVarargs + 1;
                            b = nVarargs;
                        }
                        if (b > nVarargs) {
                            Array.Copy(stack, baseR - nVarargs, stack, baseR + a, nVarargs);
                            Array.Clear(stack, baseR + a + nVarargs, b - nVarargs);
                        } else {
                            Array.Copy(stack, baseR - nVarargs, stack, baseR + a, b);
                        }
                        continue;
                    }
                    case OpCode.TFORCALL: {
                        PushCallInfo();
                        var a = GetA(op);
                        R(a + 3) = R(a);
                        R(a + 4) = R(a + 1);
                        R(a + 5) = R(a + 2);
                        Call(a + 3, 3, GetC(op));
                        continue;
                    }
                    case OpCode.TFORLOOP: {
                        var value = R(GetA(op) + 1);
                        if (value != LuaValue.Nil) {
                            R(GetA(op)) = value;
                            pc += GetSbx(op);
                        }
                        continue;
                    }
                }
            }
        }
    }
}

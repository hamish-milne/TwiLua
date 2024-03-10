using System;
using static TwiLua.Instruction;
using System.Runtime.CompilerServices;

namespace TwiLua
{
    public readonly struct UpValueInfo : IEquatable<UpValueInfo>
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

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(InStack);
            hash.Add(Index);
            return hash.ToHashCode();
        }

        public bool Equals(UpValueInfo other) => Name == other.Name && InStack == other.InStack && Index == other.Index;
        public override bool Equals(object? obj) => obj is UpValueInfo info && Equals(info);
        public static bool operator ==(UpValueInfo left, UpValueInfo right) => left.Equals(right);
        public static bool operator !=(UpValueInfo left, UpValueInfo right) => !(left == right);

    }

    public readonly struct LocalVarInfo : IEquatable<LocalVarInfo>
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

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(Start);
            hash.Add(End);
            return hash.ToHashCode();
        }

        public bool Equals(LocalVarInfo other) => Name == other.Name && Start == other.Start && End == other.End;
        public override bool Equals(object? obj) => obj is LocalVarInfo info && Equals(info);
        public static bool operator ==(LocalVarInfo left, LocalVarInfo right) => left.Equals(right);
        public static bool operator !=(LocalVarInfo left, LocalVarInfo right) => !(left == right);
    }

    public sealed class LuaFunction
    {
        public int[] code = Array.Empty<int>();
        public LuaValue[] constants = Array.Empty<LuaValue>();
        public UpValueInfo[] upvalues = Array.Empty<UpValueInfo>();
        public LuaFunction[] prototypes = Array.Empty<LuaFunction>();
        public LocalVarInfo[] locals = Array.Empty<LocalVarInfo>();
        public Location[] locations = Array.Empty<Location>();
        public int nParams;
        public int nSlots;
        public string chunkName = string.Empty;
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
        public void Close(in LuaValue value) {
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

    struct CallInfo
    {
        public int resultsIdx;
        public int func;
        public int pc;
        public int top;
        public int baseR;
        public int nVarargs;
        public int expResults;
        public bool isYieldable;
    }

    public sealed class LuaThread {

        readonly LuaValue[] stack;
        readonly LuaUpValue?[] upValueStack;
        readonly CallInfo[] callStack;
        int callStackPtr;
        
        public bool IsRunning {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }
        public bool IsMain { get; }
        public bool IsDead { get; set; }
        public bool IsYieldable { get; set; }

        public int CallDepth => callStackPtr;

        public int Count => top - func;

        public void UnwindStack(int ciptr) {
            if (ciptr > callStackPtr) {
                throw new InvalidOperationException($"Tried to unwind stack to {ciptr} but it is already at {callStackPtr}");
            }
            Array.Clear(callStack, ciptr, callStackPtr - ciptr);
            callStackPtr = ciptr;
            Return(0, 1); // Reset call info vars
        }

        public void Call(int nArgs, int nReturns) {
            if (nArgs < 0 || nArgs > top - func) {
                throw new ArgumentOutOfRangeException(nameof(nArgs));
            }
            if (nReturns < 0) {
                throw new ArgumentOutOfRangeException(nameof(nReturns));
            }
            var callee = top - nArgs - 1;
            var stopAt = callStackPtr;
            PushCallInfo();
            IsYieldable = false;
            Call(callee - baseR, nArgs + 1, nReturns + 1, isTailCall: false);
            Execute(stopAt);
        }

        public int Callback(int callee, int nArgs, int resultsIdx) {
            callee--;
            resultsIdx--;
            var stopAt = callStackPtr;
            PushCallInfo();
            IsYieldable = false;
            this.resultsIdx = baseR + resultsIdx;
            Call(callee, nArgs, 0, isTailCall: true);
            Execute(stopAt);
            return top - callee - baseR;
        }

        public void Yield() {
            if (!IsYieldable) {
                throw new LuaRuntimeError("Cannot yield from this coroutine");
            }
            IsRunning = false;
        }

        int resultsIdx;
        int func;
        int pc;
        int top;
        int baseR;
        int nVarargs;
        int expResults;

        int[] code = null!;
        LuaUpValue[] parentUpValues = null!;
        LuaValue[] constants = null!;
        Location[]? locations;
        int nSlots;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                locations = closure.Function.locations;
            }
        }

        public LuaThread(bool isMain = true, int stackSize = 1024, int callStackSize = 256) {
            IsMain = isMain;
            stack = new LuaValue[stackSize];
            upValueStack = new LuaUpValue[stackSize];
            callStack = new CallInfo[callStackSize];
        }

        public ref LuaValue this[int n] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref stack[func + n];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Number(int idx = 1) => this[idx].ExpectNumber();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Integer(int idx = 1) => this[idx].ExpectInteger();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string String(int idx = 1) => this[idx].ExpectString();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuaTable Table(int idx = 1) => this[idx].ExpectTable();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Userdata<T>(int idx = 1) where T : IUserdata => this[idx].ExpectUserdata<T>();
        public int Return(in LuaValue v) {
            this[0] = v;
            return 1;
        }

        public void SetMainFunction(LuaClosure mainFunction) {
            if (callStackPtr != 0) {
                throw new InvalidOperationException();
            }
            stack[0] = mainFunction;
            SetFunc(0);
            top = 1;
        }

        public void Push(in LuaValue arg) {
            stack[top++] = arg;
        }

        public LuaValue Pop() {
            return stack[--top];
        }

        public LuaValue[] Execute(LuaClosure mainFunction, params LuaValue[] args) {
            SetMainFunction(mainFunction);
            foreach (var arg in args) {
                Push(arg);
            }
            IsYieldable = false;
            Resume();
            return GetResults();
        }

        public bool Resume() {
            if (callStackPtr == 0) {
                PushCallInfo();
                Call(0, top, 0, isTailCall: false);
            } else {
                Return(func, 0);
            }
            return Run();
        }

        private bool Run() {
            var ci = callStackPtr;
            try {
                Execute(ci - 1);
                IsDead = callStackPtr == 0;
                return true;
            } catch (Exception e) {
                var err = e is LuaRuntimeError lerror ? lerror.Value : e.Message;
                var location = locations?[pc - 1];
                UnwindStack(ci);
                this[0] = err;
                IsDead = true;
                throw new Exception($"At {location}: {err}", e);
                // return false;
            } finally {
                IsRunning = false;
            }
        }

        public LuaValue[] GetResults() {
            var nResults = top;
            var results = new LuaValue[nResults];
            Array.Copy(stack, resultsIdx, results, 0, nResults);
            Array.Clear(stack, resultsIdx, nResults);
            return results; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushCallInfo() {
            if (callStackPtr >= callStack.Length) {
                throw new LuaRuntimeError("Call stack overflow");
            }
            callStack[callStackPtr++] = new () {
                resultsIdx = resultsIdx,
                func = func,
                pc = pc,
                baseR = baseR,
                top = top,
                nVarargs = nVarargs,
                expResults = expResults,
                isYieldable = IsYieldable
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
        internal void Call(int callee, int nArgs, int callerResults, bool isTailCall) {
            SetFunc(baseR + callee);
            if (!isTailCall) {
                resultsIdx = func;
            }
            expResults = callerResults;
            baseR = func + 1;
            if (nArgs == 0) {
                nArgs = top - baseR;
            } else {
                nArgs--;
            }
            switch (stack[func].Object) {
            case LuaClosure closure:
                var function = closure.Function;
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
            case LuaCFunction cFunction: {
                top = func + nArgs;
                nSlots = nArgs;
                var nReturns = cFunction.Invoke(this);
                if (!IsRunning) {
                    // Yield was called
                    Array.Clear(stack, func, nArgs);
                    top = func;
                    return;
                }
                Return(func, nReturns + 1);
                break;
            }
            case IUserdata userdata: {
                top = func + nArgs;
                nSlots = nArgs;
                var nReturns = userdata.Call(this);
                if (!IsRunning) {
                    // Yield was called
                    Array.Clear(stack, func, nArgs);
                    top = func;
                    return;
                }
                Return(func, nReturns + 1);
                break;
            }
            default:
                throw new Exception($"Tried to call `{stack[func]}` which isn't a function");
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

            Array.Copy(stack, src, stack, resultsIdx, nResults);
            var clearFrom = resultsIdx + nResults;
            var clearTo = func + nSlots;
            if (clearTo >= clearFrom) {
                Array.Clear(stack, clearFrom, clearTo - clearFrom + 1);
            }

            top = resultsIdx + nResults;
            pc = callInfo.pc;
            SetFunc(callInfo.func);
            baseR = callInfo.baseR;
            nVarargs = callInfo.nVarargs;
            expResults = callInfo.expResults;
            resultsIdx = callInfo.resultsIdx;
            IsYieldable = callInfo.isYieldable;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CallMeta2(int op, in LuaValue metaFn, in LuaValue arg1, in LuaValue arg2) {
            PushCallInfo();
            R(nSlots + 0) = metaFn;
            R(nSlots + 1) = arg1;
            R(nSlots + 2) = arg2;
            resultsIdx = baseR + GetA(op);
            Call(nSlots, 3, 2, isTailCall: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetTable(int op, in LuaValue table1) {
            var key = RK(GetC(op));
            if (table1.Userdata != null) {
                R(GetA(op)) = table1.Userdata.Index(this, key);
                return;
            }
            var table = table1.ExpectTable();
            while (!table.TryGetValue(key, out R(GetA(op))) && table.MetaTable != null) {
                var index = table.MetaTable["__index"];
                if (index.Object is LuaTable table2) {
                    table = table2;
                } else {
                    CallMeta2(op, index, table, key);
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetTable(int op, in LuaValue table1) {
            var key = RK(GetB(op));
            if (table1.Userdata != null) {
                table1.Userdata.NewIndex(this, key, RK(GetC(op)));
                return;
            }
            var table = table1.Table ?? throw new Exception();
            while (!table.TryGetValue(key, out var _) && table.MetaTable != null) {
                var index = table.MetaTable["__newindex"];
                if (index.Object is LuaTable table2) {
                    table = table2;
                } else {
                    PushCallInfo();
                    R(nSlots + 0) = index;
                    R(nSlots + 1) = table;
                    R(nSlots + 2) = key;
                    R(nSlots + 3) = RK(GetC(op));
                    Call(nSlots, 4, 1, isTailCall: false);
                    return;
                }
            }
            table[key] = RK(GetC(op));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Arithmetic(int op, string method, out double n1, out double n2) {
            var b = RK(GetB(op));
            var c = RK(GetC(op));
            if (b.TryGetNumber(out n1) && c.TryGetNumber(out n2)) {
                return true;
            } else if (b.TryGetMetaValue(method, out var meta)) {
                CallMeta2(op, meta, b, c);
                n1 = n2 = default;
                return false;
            } else if (c.TryGetMetaValue(method, out meta)) {
                CallMeta2(op, meta, c, b);
                n1 = n2 = default;
                return false;
            } else {
                throw new LuaRuntimeError($"attempt to perform arithmetic on `{b}` and `{c}`");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Bitwise(int op, string method, out long n1, out long n2) {
            var b = RK(GetB(op));
            var c = RK(GetC(op));
            if (b.TryGetInteger(out n1) && c.TryGetInteger(out n2)) {
                return true;
            } else if (b.TryGetMetaValue(method, out var meta)) {
                CallMeta2(op, meta, b, c);
                n1 = n2 = default;
                return false;
            } else if (c.TryGetMetaValue(method, out meta)) {
                CallMeta2(op, meta, c, b);
                n1 = n2 = default;
                return false;
            } else {
                throw new LuaRuntimeError($"attempt to perform arithmetic on `{b}` and `{c}`");
            }
        }

        double n1, n2;
        long i1, i2;

        private void Execute(int stopAt) {
            IsRunning = true;
            _Execute(stopAt);
        }

#if NET5_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private void _Execute(int stopAt)
        {
            while (IsRunning) {
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
                        GetTable(op, UpVal(GetB(op)));
                        break;
                    case OpCode.GETTABLE:
                        GetTable(op, R(GetB(op)));
                        break;
                    case OpCode.SETTABUP:
                        SetTable(op, UpVal(GetA(op)));
                        continue;
                    case OpCode.SETUPVAL:
                        UpVal(GetB(op)) = R(GetA(op));
                        continue;
                    case OpCode.SETTABLE:
                        SetTable(op, R(GetA(op)));
                        continue;
                    case OpCode.NEWTABLE:
                        R(GetA(op)) = new LuaTable(GetB(op));
                        continue;
                    case OpCode.SELF: {
                        // We need to copy 'b' here because it might be overwritten
                        var b = R(GetB(op));
                        R(GetA(op) + 1) = b;
                        GetTable(op, b);
                        continue;
                    }
                    case OpCode.ADD:
                        if (Arithmetic(op, "__add", out n1, out n2)) {
                            R(GetA(op)) = n1 + n2;
                        }
                        continue;
                    case OpCode.SUB:
                        if (Arithmetic(op, "__sub", out n1, out n2)) {
                            R(GetA(op)) = n1 - n2;
                        }
                        continue;
                    case OpCode.MUL:
                        if (Arithmetic(op, "__mul", out n1, out n2)) {
                            R(GetA(op)) = n1 * n2;
                        }
                        continue;
                    case OpCode.MOD:
                        if (Arithmetic(op, "__mod", out n1, out n2)) {
                            R(GetA(op)) = n1 % n2;
                        }
                        continue;
                    case OpCode.POW:
                        if (Arithmetic(op, "__pow", out n1, out n2)) {
                            R(GetA(op)) = Math.Pow(n1, n2);
                        }
                        continue;
                    case OpCode.DIV:
                        if (Arithmetic(op, "__div", out n1, out n2)) {
                            R(GetA(op)) = n1 / n2;
                        }
                        continue;
                    case OpCode.IDIV:
                        if (Arithmetic(op, "__idiv", out n1, out n2)) {
                            R(GetA(op)) = Math.Floor(n1 / n2);
                        }
                        continue;
                    case OpCode.BAND:
                        if (Bitwise(op, "__band", out i1, out i2)) {
                            R(GetA(op)) = i1 & i2;
                        }
                        continue;
                    case OpCode.BOR:
                        if (Bitwise(op, "__bor", out i1, out i2)) {
                            R(GetA(op)) = i1 | i2;
                        }
                        continue;
                    case OpCode.BXOR:
                        if (Bitwise(op, "__bxor", out i1, out i2)) {
                            R(GetA(op)) = i1 ^ i2;
                        }
                        continue;
                    case OpCode.SHL:
                        if (Bitwise(op, "__shl", out i1, out i2)) {
                            R(GetA(op)) = i1 << (int)i2;
                        }
                        continue;
                    case OpCode.SHR:
                        if (Bitwise(op, "__shr", out i1, out i2)) {
                            R(GetA(op)) = i1 >> (int)i2;
                        }
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
                        Call(GetA(op), GetB(op), GetC(op), isTailCall: false);
                        continue;
                    case OpCode.TAILCALL: {
                        if ( R(GetA(op)).Object is not LuaClosure ) {
                            goto case OpCode.CALL;
                        }
                        Close(baseR);
                        Call(GetA(op), GetB(op), GetC(op), isTailCall: true);
                        continue;
                    }
                    case OpCode.RETURN: {
                        Return(baseR + GetA(op), GetB(op));
                        if (callStackPtr == stopAt) {
                            return;
                        }
                        continue;
                    }
                    case OpCode.FORLOOP: {
                        var step = (int)R(GetA(op) + 2).ExpectNumber();
                        var limit = (int)R(GetA(op) + 1).ExpectNumber();
                        var i = R(GetA(op)).Number + step;
                        R(GetA(op)) = i;
                        if ( (step > 0 && i <= limit) || (step < 0 && i >= limit) ) {
                            pc += GetSbx(op);
                            R(GetA(op) + 3) = i;
                        }
                        continue;
                    }
                    case OpCode.FORPREP:
                        R(GetA(op)) = (int)R(GetA(op)).ExpectNumber() - (int)R(GetA(op) + 2).ExpectNumber();
                        pc += GetSbx(op);
                        continue;
                    case OpCode.SETLIST: {
                        var list = R(GetA(op)).ExpectTable();
                        var block = GetC(op) - 1;
                        if (block < 0) {
                            block = code[pc++];
                        }
                        var n = GetB(op);
                        if ( n <= 0 ) {
                            n = top - GetA(op);
                        }
                        while ( (block*Lua.FieldsPerFlush + n) > list.Length ) {
                            list.Add(LuaValue.Nil);
                        }
                        for ( var i = 1; i <= n; i++ ) {
                            list[block*Lua.FieldsPerFlush + i] = R(GetA(op) + i);
                        }
                        continue;
                    }
                    case OpCode.CLOSURE: {
                        var protoIdx = GetBx(op);
                        var proto = stack[func].Function!.Function.prototypes[protoIdx];
                        var upValues = new LuaUpValue[proto.upvalues.Length];
                        for (int i = 0; i < upValues.Length; i++) {
                            var upValInfo = proto.upvalues[i];
                            if (upValInfo.InStack) {
                                var idx = baseR + upValInfo.Index;
                                var upValObj = upValueStack[idx];
                                if (upValObj == null) {
                                    upValueStack[idx] = upValObj = new () { Index = idx };
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
                        Call(a + 3, 3, GetC(op), isTailCall: false);
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

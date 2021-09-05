using System.Collections.Generic;
using System;
using static YANCL.Instruction;
using System.Runtime.CompilerServices;

namespace YANCL
{

    public struct LuaUpValue {
        public bool InStack;
        public int Index;
    }

    public class LuaFunction {
        public int[] code;
        public int entry;
        public LuaValue[] constants;
        public LuaUpValue[] upvalues;
        public LuaFunction[] prototypes;
        public int nParams;
        public int nLocals;
        public int nSlots;
        public int StackSize => nParams + nLocals + nSlots;
    }

    public class LuaClosure {
        public readonly LuaFunction Function;
        public readonly LuaClosure? Parent;
        public LuaValue[]? Stack;

        public LuaClosure(LuaFunction function, LuaClosure? parent) {
            Function = function;
            Parent = parent;
        }
    }

    struct CallInfo {
        public int func;
        public int pc;
        public int top;
        public int baseR;
        public int expectedResults;
        public int closureCount;
    }

    public class LuaState {

        readonly LuaValue[] stack;
        readonly CallInfo[] callStack;
        int callStackPtr;
        readonly Stack<LuaClosure> closures = new Stack<LuaClosure>();
        LuaClosure closure = null!;

        int func;
        int pc;
        int top;
        int baseR;
        int expectedResults;
        int closureCount;

        public LuaState(int stackSize, int callStackSize) {
            stack = new LuaValue[stackSize];
            callStack = new CallInfo[callStackSize];
        }

        public LuaValue[] Execute(LuaFunction function, params LuaValue[] args) {
            func = 0;
            pc = function.entry;
            stack[0] = new LuaClosure(function, null);
            for (int i = 0; i < args.Length; i++) {
                stack[i + 1] = args[i];
            }
            callStack[0] = new CallInfo();
            callStackPtr = 1;
            Call(Instruction.Build3(OpCode.CALL, 0, args.Length + 1, 0));
            Execute();
            var results = new LuaValue[top];
            for (int i = 0; i < top; i++) {
                results[i] = stack[i];
            }
            return results; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LuaValue R(int i) => ref stack[baseR + i];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LuaValue RK(int idx) {
            if ((idx & KFlag) != 0) {
                return ref closure.Function.constants[idx & 0xFF];
            } else {
                return ref stack[baseR + idx];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LuaValue UpVal(int i) {
            var c = closure;
            do {
                if (c.Function.upvalues[i].InStack) {
                    return ref stack[c.Function.upvalues[i].Index];
                }
                i = c.Function.upvalues[i].Index;
                c = c.Parent!;
            } while (c != null);
            throw new Exception("Invalid upvalue");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Call(int op) {
            func = GetA(op);
            closure = stack[func].Function!;
            int nArgs = GetB(op);
            if (nArgs == 0) {
                nArgs = top - func - 1;
            } else {
                nArgs--;
            }
            while (nArgs < closure.Function.nParams) {
                stack[func + nArgs++] = LuaValue.Nil;
            }
            expectedResults = GetC(op);
            pc = closure.Function.entry;
            top = func + nArgs + closure.Function.nLocals + closure.Function.nSlots + 1;
            baseR = top - closure.Function.StackSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Close() {
            if (closureCount > 0) {
                var closedStack = new LuaValue[closure.Function.nParams + closure.Function.nLocals];
                for (int i = 0; i < closedStack.Length; i++) {
                    closedStack[i] = R(i);
                }
                while (closureCount > 0) {
                    closureCount--;
                    closures.Pop().Stack = closedStack;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Execute()
        {
            closure = stack[func].Function!;

            while (true) {
                int op = closure.Function.code[pc++];
                switch (GetOpCode(op)) {
                    case OpCode.MOVE:
                        R(GetA(op)) = R(GetB(op));
                        break;
                    case OpCode.LOADK:
                        R(GetA(op)) = closure.Function.constants[GetBx(op)];
                        break;
                    case OpCode.LOADBOOL:
                        R(GetA(op)) = (GetB(op) != 0);
                        if (GetC(op) != 0) {
                            pc++;
                        }
                        break;
                    case OpCode.LOADNIL:
                        for (int a = GetA(op), b = GetB(op); a <= b; a++) {
                            stack[baseR + a] = LuaValue.Nil;
                        }
                        break;
                    case OpCode.GETUPVAL:
                        R(GetA(op)) = UpVal(GetB(op));
                        break;
                    case OpCode.GETTABUP:
                        R(GetA(op)) = UpVal(GetB(op))[RK(GetC(op))];
                        break;
                    case OpCode.GETTABLE:
                        R(GetA(op)) = RK(GetB(op))[RK(GetC(op))];
                        break;
                    case OpCode.SETTABUP:
                        UpVal(GetA(op))[RK(GetB(op))] = RK(GetC(op));
                        break;
                    case OpCode.SETUPVAL:
                        UpVal(GetB(op)) = R(GetA(op));
                        break;
                    case OpCode.SETTABLE:
                        RK(GetA(op))[RK(GetB(op))] = RK(GetC(op));
                        break;
                    case OpCode.NEWTABLE:
                        R(GetA(op)) = LuaValue.NewTable();
                        break;
                    case OpCode.SELF:
                        R(GetA(op)) = R(GetB(op))[RK(GetC(op))];
                        R(GetA(op) + 1) = R(GetB(op));
                        break;
                    case OpCode.ADD:
                        R(GetA(op)) = (RK(GetB(op)).Number + RK(GetC(op)).Number);
                        break;
                    case OpCode.SUB:
                        R(GetA(op)) = (RK(GetB(op)).Number - RK(GetC(op)).Number);
                        break;
                    case OpCode.MUL:
                        R(GetA(op)) = (RK(GetB(op)).Number * RK(GetC(op)).Number);
                        break;
                    case OpCode.MOD:
                        R(GetA(op)) = (RK(GetB(op)).Number % RK(GetC(op)).Number);
                        break;
                    case OpCode.POW:
                        R(GetA(op)) = (Math.Pow(RK(GetB(op)).Number, RK(GetC(op)).Number));
                        break;
                    case OpCode.DIV:
                        R(GetA(op)) = (RK(GetB(op)).Number / RK(GetC(op)).Number);
                        break;
                    case OpCode.IDIV:
                        R(GetA(op)) = (Math.Floor(RK(GetB(op)).Number / RK(GetC(op)).Number));
                        break;
                    case OpCode.BAND:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number & (long)RK(GetC(op)).Number);
                        break;
                    case OpCode.BOR:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number | (long)RK(GetC(op)).Number);
                        break;
                    case OpCode.BXOR:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number ^ (long)RK(GetC(op)).Number);
                        break;
                    case OpCode.SHL:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number << (int)RK(GetC(op)).Number);
                        break;
                    case OpCode.SHR:
                        R(GetA(op)) = ((long)RK(GetB(op)).Number >> (int)RK(GetC(op)).Number);
                        break;
                    case OpCode.UNM:
                        R(GetA(op)) = -R(GetB(op)).Number;
                        break;
                    case OpCode.BNOT:
                        R(GetA(op)) = ~(long)RK(GetB(op)).Number;
                        break;
                    case OpCode.NOT:
                        R(GetA(op)) = !R(GetB(op)).Boolean;
                        break;
                    case OpCode.LEN:
                        R(GetA(op)) = R(GetB(op)).Length;
                        break;
                    case OpCode.CONCAT: {
                        var b = GetB(op);
                        var sb = new string[GetC(op) - b + 1];
                        for (int i = 0; i < sb.Length; i++) {
                            sb[i] = R(b + i).String!;
                        }
                        R(GetA(op)) = string.Concat(sb);
                        break;
                    }
                    case OpCode.JMP:
                        pc += GetSbx(op);
                        break;
                    case OpCode.EQ:
                        if ( (RK(GetB(op)) == RK(GetC(op))) != (GetA(op) != 0) ) {
                            pc++;
                        }
                        break;
                    case OpCode.LT:
                        if ( (RK(GetB(op)).Number < RK(GetC(op)).Number) != (GetA(op) != 0) ) {
                            pc++;
                        }
                        break;
                    case OpCode.LE:
                        if ( (RK(GetB(op)).Number <= RK(GetC(op)).Number) != (GetA(op) != 0) ) {
                            pc++;
                        }
                        break;
                    case OpCode.TEST:
                        if ( R(GetA(op)).Boolean != (GetC(op) != 0) ) {
                            pc++;
                        }
                        break;
                    case OpCode.TESTSET:
                        if ( R(GetB(op)).Boolean != (GetC(op) != 0) ) {
                            pc++;
                        } else {
                            R(GetA(op)) = R(GetB(op));
                        }
                        break;
                    case OpCode.CALL:
                        callStack[callStackPtr++] = new CallInfo {
                            func = func,
                            pc = pc,
                            baseR = baseR,
                            top = top,
                            expectedResults = expectedResults,
                            closureCount = closureCount,
                        };
                        closureCount = 0;
                        Call(op);
                        break;
                    case OpCode.TAILCALL:
                        Close();
                        Call(op);
                        break;
                    case OpCode.RETURN: {
                        int nResults = GetB(op);
                        if (nResults == 0) {
                            nResults = top - GetA(op);
                        } else {
                            nResults--;
                        }
                        var returns = GetA(op) + baseR;
                        for (int i = 0; i < nResults; i++) {
                            stack[func + i] = stack[returns + i];
                        }
                        for (int i = func + nResults; i < top; i++) {
                            stack[i] = LuaValue.Nil;
                        }
                        pc = callStack[--callStackPtr].pc;
                        func = callStack[callStackPtr].func;
                        closure = stack[func].Function!;
                        baseR = callStack[callStackPtr].baseR;
                        if (expectedResults == 0) {
                            top = func + nResults;
                        } else {
                            top = callStack[callStackPtr].top;
                        }
                        expectedResults = callStack[callStackPtr].expectedResults;
                        closureCount = callStack[callStackPtr].closureCount;
                        if (callStackPtr == 0) {
                            return;
                        }
                        break;
                    }
                    case OpCode.FORLOOP: {
                        var i = R(GetA(op)).Number + R(GetA(op) + 2).Number;
                        R(GetA(op)) = i;
                        var step = R(GetA(op) + 2).Number;
                        var limit = R(GetA(op) + 1).Number;
                        if ( (step > 0 && i <= limit) || (step < 0 && i >= limit) ) {
                            pc += GetSbx(op);
                            R(GetA(op) + 3) = i;
                        }
                        break;
                    }
                    case OpCode.FORPREP:
                        R(GetA(op)) = (R(GetA(op)).Number - R(GetA(op) + 2).Number);
                        pc += GetSbx(op);
                        break;
                    case OpCode.SETLIST: {
                        var list = R(GetB(op)).Array!;
                        var n = GetB(op);
                        if ( n > 0 ) {
                            n = top - GetA(op);
                        }
                        while ( n > list.Count ) {
                            list.Add(LuaValue.Nil);
                        }
                        for ( var i = 0; i < n; i++ ) {
                            list[GetC(op)*50 + i] = R(GetA(op) + i);
                        }
                        break;
                    }
                    case OpCode.CLOSURE: {
                        var proto = closure.Function.prototypes[GetBx(op)];
                        var newClosure = new LuaClosure(proto, closure);
                        closures.Push(newClosure);
                        closureCount++;
                        R(GetA(op)) = newClosure;
                        break;
                    }
                    case OpCode.VARARG: {
                        var nArgs = baseR - func - 1;
                        var a = GetA(op);
                        var nRegs = top - (baseR + a);
                        var nCopy = GetB(op);
                        if ( nCopy == 0 ) {
                            nCopy = nArgs > nRegs ? nRegs : nArgs;
                        }
                        for ( var i = 0; i < nCopy; i++ ) {
                            R(a + i) = stack[func + i];
                        }
                        for ( var i = nCopy; i < nRegs; i++ ) {
                            R(a + i) = LuaValue.Nil; 
                        }
                        break;
                    }
                }
            }
        }
    }
}

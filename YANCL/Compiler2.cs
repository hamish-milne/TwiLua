

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    internal class Compiler2
    {
        enum ILOP {
            Upvalue,
            Constant,
            Local,
            // Condition,
            Vararg,
            Call,
            NewTable,
            GetTable,
            GetTabUp

            // Binary,
            // Unary,
            // Test,
            // Logical,
            // Concat,
        }

        enum TargetType {
            Local,
            Upvalue,
            Index,
            UpvalIndex,
        }

        struct IL {
            public ILOP ilop;
            public int a, b;
            public TokenType token;
            public LuaValue value;
        }

        struct Target {
            public TargetType type;
            public int a, b;
        }

        readonly Stack<IL> il = new Stack<IL>();
        readonly Stack<Target> targets = new Stack<Target>();
        readonly List<int> code = new List<int>();
        readonly List<LuaValue> constants = new List<LuaValue>();

        int locals;
        int tmps;
        int maxStack;

        public LuaFunction MakeFunction() {
            return new LuaFunction {
                code = code.ToArray(),
                constants = constants.ToArray(),
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = Array.Empty<LuaFunction>(),
                nLocals = locals,
                nSlots = maxStack - locals,
            };
        }

        public void PushTarget() {
            var inst = il.Pop();
            targets.Push(new Target {
                a = inst.a,
                b = inst.b,
                type = inst.ilop switch {
                    ILOP.Local => TargetType.Local,
                    ILOP.Upvalue => TargetType.Upvalue,
                    ILOP.GetTable => TargetType.Index,
                    ILOP.GetTabUp => TargetType.UpvalIndex,
                    _ => throw new InvalidOperationException()
                }
            });
        }

        public void Assign() {
            if (targets.Count-1 > tmps) {
                PushArg();
                code.Add(Build2(LOADNIL, Top, Top + (targets.Count - il.Count) - 1));
            }
            while (targets.Count > 0) {
                var t = targets.Pop();
                switch (t.type) {
                    case TargetType.Local:
                        WriteIL(il.Pop(), t.a);
                        PopS(t.a);
                        break;
                    case TargetType.Upvalue:
                        code.Add(Build2(SETUPVAL, t.a, PopRK()));
                        break;
                    case TargetType.Index:
                        code.Add(Build3(SETTABLE, t.a, t.b, PopRK()));
                        PopS(t.b);
                        PopS(t.a);
                        break;
                    case TargetType.UpvalIndex:
                        code.Add(Build3(SETTABUP, t.a, t.b, PopRK()));
                        PopS(t.b);
                        break;
                }
            }
        }

        public void Upvalue(int idx) {
            il.Push(new IL {
                ilop = ILOP.Upvalue,
                a = idx
            });
        }

        public void Local(int idx) {
            il.Push(new IL {
                ilop = ILOP.Local,
                a = idx
            });
        }

        // public void SetUpvalue(int idx) {
        //     var value = PopRK();
        //     code.Add(Build2(SETUPVAL, idx, value));
        // }

        public void Index() {
            var idx = PushRK();
            if (il.Peek().ilop == ILOP.Upvalue) {
                il.Push(new IL{ilop = ILOP.GetTabUp, a = il.Pop().a, b = idx});
            } else {
                il.Push(new IL{ilop = ILOP.GetTable, a = PushRK(), b = idx});
            }
        }

        // private bool TryConstantBinary(TokenType token) {
        //     if (il.Peek().ilop != ILOP.Constant) {
        //         return false;
        //     }
        //     var cb = il.Pop().value.Number;
        //     if (il.Peek().ilop != ILOP.Constant) {
        //         il.Push(new IL{ilop = ILOP.Constant, value = cb});
        //         return false;
        //     }
        //     var ca = il.Pop().value.Number;
        //     var cr = token switch {
        //         TokenType.Plus => ca + cb,
        //         TokenType.Minus => ca - cb,
        //         TokenType.Star => ca * cb,
        //         TokenType.Slash => ca / cb,
        //         TokenType.Percent => ca % cb,
        //         TokenType.Caret => Math.Pow(ca, cb),
        //     };
        //     il.Push(new IL{ilop = ILOP.Constant, value = cr});
        //     return true;
        // }

        // private bool TryConstantUnary(TokenType token) {
        //     if (il.Peek().ilop != ILOP.Constant) {
        //         return false;
        //     }
        //     var ca = il.Pop().value;
        //     var cr = token switch {
        //         TokenType.Minus => -ca.Number,
        //         TokenType.Tilde => ~(long)ca.Number,
        //     };
        //     il.Push(new IL{ilop = ILOP.Constant, value = cr});
        //     return true;
        // }

        public void Binary(TokenType token) {
            // if (TryConstantBinary(token)) {
            //     return;
            // }
            // var b = PopRK();
            // var a = PopRK();
            // il.Push(new IL {
            //     ilop = ILOP.Binary,
            //     token = token,
            //     a = a,
            //     b = b
            // });
        }

        public void Unary(TokenType token) {
            // if (TryConstantUnary(token)) {
            //     return;
            // }
            // var a = PopRK();
            // il.Push(new IL {
            //     ilop = ILOP.Unary,
            //     token = token,
            //     a = a
            // });
        }

        public void Constant(LuaValue value) {
            il.Push(new IL {
                ilop = ILOP.Constant,
                value = value
            });
        }

        public void SetLocal(int idx) {
            WriteIL(il.Pop(), idx);
        }

        public void InitLocals(int count) {
            var nils = count - tmps;
            if (nils < 0) {
                throw new InvalidOperationException();
            }
            if (nils > 0) {
                code.Add(Build2(LOADNIL, Top, Top + nils - 1));
            }
            locals += count;
            tmps -= (count - nils);
            maxStack = Math.Max(maxStack, Top);
        }

        public void Call() {
            switch (il.Peek().ilop) {
                case ILOP.Vararg:
                    code.Add(Build2(VARARG, 0, 0));
                    break;
                case ILOP.Call:
                    code.Add(Build3(CALL, 0, 0, 0));
                    break;
                default:
                    PushArg();
                    break;
            }
            il.Push(new IL {
                ilop = ILOP.Call
            });
        }

        public void Vararg() {
            il.Push(new IL{ilop = ILOP.Vararg});
        }

        public void Discard() {
            switch (il.Peek().ilop) {
                case ILOP.Call:
                    il.Pop();
                    code.Add(Build3(CALL, 0, 0, 0));
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        public void Closure(LuaFunction function) {

        }

        public void NewTable() {
            var a = Top;
            code.Add(Build3(NEWTABLE, a, 0, 0));
            il.Push(new IL{ilop = ILOP.NewTable, a = a});
        }

        public void SetList() {

        }

        // OpCode? GetBinaryOp(TokenType token) {
        //     return token switch {
        //         TokenType.Plus => ADD,
        //         TokenType.Minus => SUB,
        //         TokenType.Pipe => BOR,
        //         TokenType.Tilde => BXOR,
        //         TokenType.Ampersand => BAND,
        //         TokenType.Star => MUL,
        //         TokenType.Slash => DIV,
        //         TokenType.DoubleSlash => IDIV,
        //         TokenType.Percent => MOD,
        //         TokenType.Caret => POW,
        //         _ => null
        //     };
        // }

        // OpCode? GetTestOp(TokenType token, out bool swap, out bool invert) {
        //     swap = false;
        //     invert = false;
        //     switch (token) {
        //         case TokenType.LessThan:
        //             return LT;
        //         case TokenType.GreaterThan:
        //             swap = true;
        //             return LT;
        //         case TokenType.LessThanEqual:
        //             return LE;
        //         case TokenType.GreaterThanEqual:
        //             swap = true;
        //             return LE;
        //         case TokenType.DoubleEqual:
        //             return EQ;
        //         case TokenType.NotEqual:
        //             invert = true;
        //             return EQ;
        //         default:
        //             return null;
        //     }
        // }

        public void PushArg() {
            WriteIL(il.Pop(), null);
        }

        int PushRK() {
            var inst = il.Pop();
            switch (inst.ilop) {
                case ILOP.Constant:
                    return GetK(inst.value) | KFlag;
                case ILOP.Local:
                    return inst.a;
                default:
                    WriteIL(inst, null);
                    return Top-1;
            }
        }

        int PopRK() {
            var a = PushRK();
            PopS(a);
            return a;
        }

        void PopS(int s) {
            if (s >= locals && s == Top-1) {
                tmps--;
                if (tmps < 0) {
                    throw new InvalidOperationException();
                }
            }
            // if (il.Count > 0 && il.Peek().ilop == ILOP.Stack && il.Peek().a == s) {
            //     il.Pop();
            // }
        }

        int Top => locals + tmps; //il.Count(x => x.ilop == ILOP.Stack);

        int PushS() {
            var a = Top;
            // il.Push(new IL{ilop = ILOP.Stack, a = a});
            tmps++;
            maxStack = Math.Max(maxStack, Top);
            return a;
        }

        void WriteIL(IL inst, int? dst) {
            switch (inst.ilop) {
                case ILOP.Local:
                // case ILOP.Stack:
                    if (dst != inst.a) {
                        PopS(inst.a);
                        code.Add(Build2(MOVE, dst ?? PushS(), inst.a));
                    }
                    break;
                case ILOP.Upvalue:
                    code.Add(Build2(GETUPVAL, dst ?? PushS(), inst.a));
                    break;
                case ILOP.Call:
                    code.Add(Build3(CALL, 0, 0, 2)); // TODO
                    break;
                case ILOP.Vararg:
                    code.Add(Build2(VARARG, dst ?? PushS(), 2));
                    break;
                case ILOP.GetTable:
                    PopS(inst.b);
                    PopS(inst.a);
                    code.Add(Build3(GETTABLE, dst ?? PushS(), inst.a, inst.b));
                    break;
                case ILOP.GetTabUp:
                    PopS(inst.b);
                    code.Add(Build3(GETTABUP, dst ?? PushS(), inst.a, inst.b));
                    break;
                case ILOP.Constant:
                    switch (inst.value.Type) {
                        case LuaType.NIL:
                            code.Add(Build2(LOADNIL, dst ?? PushS(), 1));
                            break;
                        case LuaType.BOOLEAN:
                            code.Add(Build3(LOADBOOL, dst ?? PushS(), inst.value.Boolean ? 1 : 0, 0));
                            break;
                        default:
                            code.Add(Build2x(LOADK, dst ?? PushS(), GetK(inst.value)));
                            break;
                    }
                    break;
                // case ILOP.Binary:
                //     code.Add(Build3(GetBinaryOp(inst.token), s, inst.a, inst.b));
                //     break;
                // case ILOP.Unary:
                //     code.Add(Build2(UNM, s, inst.a));
                //     break;
                // case ILOP.Condition:
                //     code.Add(Build3(LOADBOOL, s, 0, 1));
                //     code.Add(Build3(LOADBOOL, s, 1, 0));
                //     break;
            }
        }

        int GetK(LuaValue value) {
            var k = constants.IndexOf(value);
            if (k < 0) {
                k = constants.Count;
                constants.Add(value);
            }
            return k;
        }


    }
}
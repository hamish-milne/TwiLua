using System;
using System.Collections.Generic;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    sealed class Compiler22 : ICompiler2
    {

        enum OperandType {
            Local,
            Constant,
            Upvalue,
            Call,
            Vararg,
            Expression,
            NewTable,
        }

        struct Operand {
            public OperandType Type;
            public int A, B, C, ArgsOnStack;
            public LuaValue Value;

            public override string ToString()
            {
                return Type switch {
                    OperandType.Local => $"local {A}",
                    OperandType.Constant => $"constant {Value}",
                    OperandType.Upvalue => $"upvalue {A}",
                    OperandType.Call => $"call {A}",
                    OperandType.Vararg => "vararg",
                    OperandType.Expression => $"expression {Stringify(A)}",
                    OperandType.NewTable => $"new table",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        private int K(LuaValue value) {
            var idx = constants.IndexOf(value);
            if (idx == -1) {
                constants.Add(value);
                idx = constants.Count - 1;
            }
            return idx;
        }

        private void LoadInst(Operand op, int dst) {
            if (op.Type == OperandType.Local && op.A == dst) {
                return;
            }
            if (op.Type == OperandType.Call) {
                code.Add(Build3(CALL, op.A, op.B, 2));
                if (dst != top-1) {
                    code.Add(Build2(MOVE, dst, top-1));
                }
                return;
            }
            // TODO: Don't look at previously emitted code
            if (code.Count > 0) {
                var prev = code[code.Count - 1];
                if (op.Type == OperandType.Constant && op.Value == LuaValue.Nil && GetOpCode(prev) == LOADNIL && GetA(prev)+GetB(prev)+1 == dst) {
                    code[code.Count - 1] = Build2(LOADNIL, GetA(prev), GetB(prev) + 1);
                    return;
                }
            }
            var inst = op.Type switch {
                OperandType.Local => Build2(MOVE, dst, op.A),
                OperandType.Upvalue => Build2(GETUPVAL, dst, op.A),
                OperandType.Constant => op.Value.Type switch {
                    LuaType.NIL => Build2(LOADNIL, dst, 0),
                    LuaType.BOOLEAN =>Build3(LOADBOOL, dst, op.Value.Boolean ? 1 : 0, 0),
                    _ => Build2x(LOADK, dst, K(op.Value))
                },
                OperandType.Expression => Build2x(GetOpCode(op.A), dst, GetBx(op.A)),
                OperandType.Vararg => Build2(VARARG, dst, 2),
                _ => throw new System.NotSupportedException(op.Type.ToString())
            };
            code.Add(inst);
        }

        private void StoreInst(Operand op, int src) {
            if (op.Type == OperandType.Local && op.A == src) {
                return;
            }
            var inst = op.Type switch {
                OperandType.Local => Build2(MOVE, op.A, src),
                OperandType.Upvalue => Build2(SETUPVAL, op.A, src),
                OperandType.Expression => (op.B == -1)
                    ? throw new System.NotImplementedException("Assignment to expression")
                    : Build3(GetOpCode(op.B), GetA(op.B), GetB(op.B), src),
                _ => throw new System.NotSupportedException(op.Type.ToString())
            };
            code.Add(inst);
        }

        private readonly List<LuaValue> constants = new List<LuaValue>();
        private readonly List<int> code = new List<int>();
        private readonly List<Operand> operands = new List<Operand>();
        private readonly List<LuaFunction> closures = new List<LuaFunction>();

        private int top;
        private int maxStack;

        public void SetParameters(int count) {
            AdjustTop(count);
        }

        private int PushS() {
            var r = top++;
            maxStack = Math.Max(maxStack, top);
            return r;
        }

        private void AdjustTop(int by) {
            top += by;
            maxStack = Math.Max(maxStack, top);
        }

        public void Constant(LuaValue value)
        {
            operands.Add(new Operand {
                Type = OperandType.Constant,
                Value = value,
            });
        }

        public void Argument()
        {
            var op = Pop();
            LoadInst(op, PushS());
        }

        public void Assign(int arguments, int targets)
        {
            if (arguments > 0 && PushVarargs(Math.Max(targets - arguments + 1, 0) + 1)) {
                if (arguments < targets) {
                    AdjustTop(targets - arguments);
                }
            } else {
                if (arguments == targets) {
                    var target = Peek(1);
                    switch (target.Type) {
                        case OperandType.Local:
                            LoadInst(Pop(), target.A);
                            break;
                        default:
                            var slots = 0;
                            StoreInst(target, PopRK(ref slots));
                            top -= slots;
                            break;
                    }
                    Pop();
                    targets--;
                } else {
                    Argument();
                    while (arguments < targets) {
                        arguments++;
                        Constant(LuaValue.Nil);
                        Argument();
                    }
                    if (arguments > targets) {
                        top -= arguments - targets;
                    }
                }
            }
            while (targets > 0) {
                targets--;
                StoreInst(Pop(), --top);
            }
        }

        public void Binary(TokenType token)
        {
            if (Peek(0).Value.Type == LuaType.NUMBER &&
                Peek(1).Value.Type == LuaType.NUMBER
            ) {
                var b = Pop().Value.Number;
                var a = Pop().Value.Number;
                double? result = token switch {
                    TokenType.Plus => a + b,
                    TokenType.Minus => a - b,
                    TokenType.Star => a * b,
                    TokenType.Slash => a / b,
                    TokenType.Percent => a % b,
                    TokenType.Caret => System.Math.Pow(a, b),
                    _ => null
                };
                operands.Add(new Operand {
                    Type = OperandType.Constant,
                    Value = new LuaValue(result.Value),
                });
                return;
            } else {
                var inst = token switch {
                    TokenType.Plus => ADD,
                    TokenType.Minus => SUB,
                    TokenType.Star => MUL,
                    TokenType.Slash => DIV,
                    TokenType.Percent => MOD,
                    TokenType.Caret => POW,
                    _ => throw new System.NotImplementedException()
                };
                var slots = 0;
                var b = PopRK(ref slots);
                var a = PopRK(ref slots);
                operands.Add(new Operand {
                    Type = OperandType.Expression,
                    A = Build3(inst, 0, a, b),
                    B = -1,
                    ArgsOnStack = slots,
                });
            }
        }

        public void Call(int arguments)
        {   
            var b = Dispatch(arguments, 0);
            operands.Add(new Operand {
                Type = OperandType.Call,
                A = top-arguments-1,
                B = b,
                ArgsOnStack = arguments + 1,
            });
        }

        public void Discard()
        {
            if (Peek(0).Type != OperandType.Call) {
                throw new InvalidOperationException();
            }
            var op = Pop();
            code.Add(Build3(CALL, op.A, op.B, 1));
        }

        private int PopRK(ref int argsOnStack)
        {
            if (Peek(0).Type == OperandType.Constant) {
                return K(Pop().Value) | KFlag;
            } else {
                return PopR(ref argsOnStack);
            }
        }

        private int PopR(ref int argsOnStack)
        {
            var op = Pop();
            switch (op.Type) {
                case OperandType.Local:
                    argsOnStack += op.ArgsOnStack;
                    top += op.ArgsOnStack;
                    maxStack = Math.Max(maxStack, top);
                    return op.A;
                default:
                    argsOnStack++;
                    var slot = PushS();
                    LoadInst(op, slot);
                    return slot;
            }
        }

        Operand Pop() {
            var op = operands[operands.Count - 1];
            top -= op.ArgsOnStack;
            operands.RemoveAt(operands.Count - 1);
            return op;
        }

        Operand Peek(int index) {
            return operands[operands.Count - index - 1];
        }

        private void EmitOperand(int idx, bool keepUpvalues) {
            var op = Peek(idx);
            switch (op.Type) {
                case OperandType.Upvalue:
                    if (!keepUpvalues) {
                        goto case OperandType.Expression;
                    }
                    break;
                case OperandType.Expression:
                case OperandType.Vararg:
                case OperandType.Call:
                    top -= op.ArgsOnStack;
                    var slot = PushS();
                    LoadInst(op, slot);
                    operands[operands.Count - 1 - idx] = new Operand {
                        Type = OperandType.Local,
                        A = slot,
                        ArgsOnStack = 1
                    };
                    break;
            }
        }

        public void Callee() {
            Argument();
        }

        // public void Indexee() {
        //     // EmitOperand(0, keepUpvalues: true);
        // }

        public void Index()
        {
            EmitOperand(1, keepUpvalues: true);
            var argsOnStack = 0;
            var indexer = PopRK(ref argsOnStack);
            int table;
            var getter = GETTABLE;
            var setter = SETTABLE;
            switch (Peek(0).Type)
            {
                case OperandType.Upvalue:
                    table = Pop().A;
                    getter = GETTABUP;
                    setter = SETTABUP;
                    break;
                case OperandType.NewTable:
                    table = Peek(0).A;
                    break;
                default:
                    table = PopR(ref argsOnStack);
                    break;
            }
            operands.Add(new Operand {
                Type = OperandType.Expression,
                A = Build3(getter, 0, table, indexer),
                B = Build3(setter, table, indexer, 0),
                ArgsOnStack = argsOnStack,
            });
        }

        public void Local(int idx)
        {
            operands.Add(new Operand {
                Type = OperandType.Local,
                A = idx,
            });
        }

        private bool PushVarargs(int limit)
        {
            var op = Peek(0);
            switch (op.Type) {
                case OperandType.Vararg:
                    Pop();
                    code.Add(Build2(VARARG, PushS(), limit));
                    return true;
                case OperandType.Call:
                    Pop();
                    code.Add(Build3(CALL, op.A, op.B, limit));
                    PushS();
                    return true;
                default:
                    return false;
            }
        }

        private int Dispatch(int arguments, int limit) {
            bool hasDispatch = false;
            if (arguments > 0) {
                if (!PushVarargs(limit)) {
                    Argument();
                } else {
                    hasDispatch = true;
                }
            }
            return hasDispatch ? 0 : arguments+1;
        }

        public void Return(int arguments)
        {
            var b = Dispatch(arguments, 0);
            code.Add(Build2(RETURN, top-arguments, b));
            top -= arguments;
        }

        public void Upvalue(int idx)
        {
            operands.Add(new Operand {
                Type = OperandType.Upvalue,
                A = idx,
            });
        }

        public void InitLocals(int count, int arguments)
        {
            if (arguments > 0 && PushVarargs(count - arguments + 2)) {
                if (arguments < count) {
                    AdjustTop(count - arguments);
                }
            } else {
                if (arguments > 0) {
                    Argument();
                }
                while (arguments < count) {
                    arguments++;
                    Constant(LuaValue.Nil);
                    Argument();
                }
            }
        }

        public void Jump(int label)
        {
            throw new System.NotImplementedException();
        }

        public void JumpIf(int label, bool condition)
        {
            throw new System.NotImplementedException();
        }

        public int Label()
        {
            throw new System.NotImplementedException();
        }

        public void Mark(int label)
        {
            throw new System.NotImplementedException();
        }

        public void Unary(TokenType token)
        {
            if (Peek(0).Type == OperandType.Constant) {
                LuaValue? cValue = token switch {
                    TokenType.Not => !Pop().Value.Boolean,
                    TokenType.Minus => Peek(0).Value.Type switch {
                        LuaType.NUMBER => (LuaValue?)(-Pop().Value.Number),
                        _ => null
                    },
                    TokenType.Tilde => Peek(0).Value.Number % 1 == 0 ? (LuaValue?)(~(long)Pop().Value.Number) : null,
                    _ => null,
                };
                if (cValue != null) {
                    Constant(cValue.Value);
                }
            }
            var slots = 0;
            var b = PopR(ref slots);
            operands.Add(new Operand {
                Type = OperandType.Expression,
                A = Build2(token switch {
                    TokenType.Not => NOT,
                    TokenType.Minus => UNM,
                    TokenType.Tilde => BNOT,
                    TokenType.Hash => LEN,
                    _ => throw new InvalidOperationException(),
                }, 0, b),
                B = -1,
                ArgsOnStack = slots,
            });
        }

        public void Vararg()
        {
            operands.Add(new Operand {
                Type = OperandType.Vararg,
            });
        }

        public void NewTable()
        {
            var idx = PushS();
            operands.Add(new Operand {
                Type = OperandType.NewTable,
                A = idx,
                B = code.Count,
            });
            code.Add(Build3(NEWTABLE, idx, 0, 0));
        }

        public void Closure(LuaFunction function)
        {
            operands.Add(new Operand {
                Type = OperandType.Expression,
                A = Build2(CLOSURE, 0, closures.Count),
                B = -1
            });
            closures.Add(function);
        }

        public void SetList(int array, int hash)
        {
            var op = Pop();
            if (op.Type != OperandType.NewTable) {
                throw new InvalidOperationException();
            }
            if (array > 0) {
                code.Add(Build3(SETLIST, op.A, array, 1));
                top -= array;
            }
            code[op.B] = Build3(NEWTABLE, op.A, array, hash);
            operands.Add(new Operand {
                Type = OperandType.Local,
                A = op.A,
                ArgsOnStack = 1
            });
        }

        public LuaFunction MakeFunction()
        {
            return new LuaFunction {
                code = code.ToArray(),
                constants = constants.ToArray(),
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = closures.ToArray(),
                entry = 0,
                nParams = 0,
                nLocals = top,
                nSlots = maxStack - top,
            };
        }
    }
}
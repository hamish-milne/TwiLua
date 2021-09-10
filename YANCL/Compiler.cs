using System;
using System.Collections.Generic;
using System.Diagnostics;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{

    public class Compiler
    {
        readonly Lexer lexer;
        readonly List<int> code = new List<int>(1024);
        readonly Compiler? parent;
        readonly List<LuaValue> constants = new List<LuaValue>();
        readonly List<string> locals = new List<string>();
        readonly List<LuaFunction> prototypes = new List<LuaFunction>();

        LuaFunction MakeFunction() {
            return new LuaFunction {
                code = code.ToArray(),
                constants = constants.ToArray(),
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = prototypes.ToArray(),
                nLocals = locals.Count,
                nSlots = maxStack - locals.Count,
            };
        }

        Compiler(string str) {
            lexer = new Lexer(str);
        }

        int Top;
        int Head => Top - 1;
        int nSlots;
        int nResults;
        int firstResult;
        int maxStack;
        int pcAtMaxStack;

        void SetTop(int top) {
            if (top > maxStack) {
                maxStack = top;
                pcAtMaxStack = code.Count;
            }
            Top = top;
        }

        int EnvUp() => 0;

        int Push() {
            Top++;
            if (Top > maxStack) {
                maxStack = Top;
                pcAtMaxStack = code.Count;
            }
            return Head;
        }

        int PopS() {
            return --Top;
        }

        void Shrink() {
            code.RemoveAt(code.Count - 1);
            if (code.Count == pcAtMaxStack && maxStack == Top) {
                maxStack--;
                pcAtMaxStack--;
            }
            Top--;
        }

        int? TryPopRK() {
            Debug.Assert(Top > 0);
            Debug.Assert(code.Count > 0);
            var inst = code[code.Count - 1];
            switch (GetOpCode(inst)) {
                case MOVE:
                    if (GetA(inst) == Head) {
                        Shrink();
                        return GetB(inst);
                    }
                    break;
                case LOADK:
                    if (GetA(inst) == Head) {
                        Shrink();
                        return GetBx(inst) | KFlag;
                    }
                    break;
                case LOADBOOL:
                    if (GetA(inst) == Head) {
                        Shrink();
                        return Constant(GetB(inst) != 0);
                    }
                    break;
                case LOADNIL:
                    if (GetA(inst) == Head && GetB(inst) == 0) {
                        Shrink();
                        return Constant(LuaValue.Nil);
                    }
                    break;
                default:
                    break;
            }
            return null;
        }

        int PopRK() => TryPopRK() ?? PopS();

        Token Next() => lexer.Next();
        TokenType Peek() => lexer.Peek();
        string? Expect(TokenType type, string after) => lexer.Expect(type, after);
        string ParseString(char term) => lexer.ParseString(term);
        bool TryTake(TokenType type) => lexer.TryTake(type);


        public static LuaFunction Compile(string str) {
            var c = new Compiler(str);
            c.ParseChunk();
            return c.MakeFunction();
        }

        void ParseChunk() {
            while (true) {
                switch (Peek()) {
                    case TokenType.Eof:
                        return;
                    default:
                        ParseStat();
                        break;
                }
            }
        }

        void ParseStat() {
            nSlots = 0;
            nResults = 0;
            switch (Peek()) {
                case TokenType.Identifier:
                case TokenType.OpenParen:
                case TokenType.OpenBrace:
                case TokenType.Minus:
                    ParseVar(resultIdx: 0);
                    break;
                case TokenType.Do:
                    Next();
                    ParseBlock();
                    break;
                case TokenType.If:
                    throw new NotImplementedException();
                case TokenType.For:
                    throw new NotImplementedException();
                case TokenType.While:
                    throw new NotImplementedException();
                case TokenType.Local:
                    ParseLocal();
                    break;
                case TokenType.Function:
                    Next();
                    ParseFunctionDeclaration();
                    return;
                case TokenType.Return:
                    Next();
                    ParseReturn();
                    break;
                case TokenType.Semicolon:
                    Next();
                    break;
                default:
                    throw new Exception($"Unexpected token {Peek()}");
            }
        }


        void ParseFunctionDeclaration() {
            var name = Expect(TokenType.Identifier, "function declaration")!;
            var localIdx = locals.IndexOf(name);
            if (localIdx >= 0) {
                if (PeekFunctionSuffix()) {
                    code.Add(Build2(MOVE, Push(), localIdx));
                    ParseFunctionSuffix();
                } else {
                    PushFunction(hasSelf: false);
                    code.Add(Build2(MOVE, localIdx, PopS()));
                }
            } else {
                if (PeekFunctionSuffix()) {
                    code.Add(Build3(GETTABUP, Push(), EnvUp(), Constant(name)));
                    ParseFunctionSuffix();
                } else {
                    PushFunction(hasSelf: false);
                    code.Add(Build3(SETTABUP, EnvUp(), Constant(name), PopS()));
                }
            }
        }

        bool PeekFunctionSuffix() {
            switch (Peek()) {
                case TokenType.Dot:
                case TokenType.Colon:
                    return true;
                default:
                    return false;
            }
        }

        void ParseReturn() {
            var start = Top;
            int nArgs = 0;
            switch (Peek()) {
                case TokenType.End:
                case TokenType.Eof:
                case TokenType.Semicolon:
                    break;
                default:
                    nArgs = ParseArgs(0);
                    break;
            }
            code.Add(Build2(RETURN, start, nArgs + 1));
        }

        void ParseFunctionSuffix() {
            bool hasSelf;
            switch (Next().type) {
                case TokenType.Dot: hasSelf = false; break;
                case TokenType.Colon: hasSelf = true; break;
                default: throw new Exception($"Unexpected token {Peek()}");
            }
            var constIdx = Constant(Expect(TokenType.Identifier, "function suffix")!);
            if (!hasSelf && PeekFunctionSuffix()) {
                var src = PopRK();
                code.Add(Build3(GETTABLE, Push(), src, constIdx));
                ParseFunctionSuffix();
            } else {
                PushFunction(hasSelf);
                var func = PopS();
                code.Add(Build3(SETTABLE, PopS(), constIdx, func));
            }
        }

        int Constant(LuaValue value) {
            var constIdx = constants.IndexOf(value);
            if (constIdx < 0) {
                constIdx = constants.Count;
                constants.Add(value);
            }
            return constIdx | KFlag;
        }

        void PushK(LuaValue value) {
            code.Add(Build2x(LOADK, Push(), Constant(value) & ~KFlag));
        }

        bool PeekSuffix() {
            switch (Peek()) {
                case TokenType.Dot:
                case TokenType.OpenBracket:
                case TokenType.OpenParen:
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote:
                case TokenType.OpenBrace:
                case TokenType.Colon:
                    return true;
                default:
                    return false;
            }
        }

        bool PeekAssignment() => TryTake(TokenType.Equal);

        bool PeekComma() => TryTake(TokenType.Comma);

        int ParseArgs(int nArgs) {
            do {
                nArgs++;
                PushExpression();
            } while (TryTake(TokenType.Comma));
            if (ExtendVararg(0)) {
                nArgs = -1;
            }
            return nArgs;
        }

        bool ExtendVararg(int nReturns) {
            var inst = code[code.Count - 1];
            switch (Instruction.GetOpCode(inst)) {
                case CALL: {
                    var a = Instruction.GetA(inst);
                    var b = Instruction.GetB(inst);
                    code[code.Count - 1] = Build3(CALL, a, b, nReturns);
                    return true;
                }
                case VARARG: {
                    var a = Instruction.GetA(inst);
                    code[code.Count - 1] = Build2(VARARG, a, nReturns);
                    return true;
                }
            }
            return false;
        }

        void ParseAssignment(out bool allowPopRK) {
            firstResult = Top;
            nResults = 0;
            while (true) {
                nResults++;
                PushExpression();
                if (Peek() == TokenType.Comma) {
                    Next();
                } else {
                    break;
                }
            }
            if (nResults != nSlots) {
                if (nResults < nSlots) {
                    if (ExtendVararg(nSlots - nResults + 2)) {
                        nResults = nSlots;
                    }
                }
                if (nResults < nSlots) {
                    code.Add(Build2(LOADNIL, firstResult + nResults, firstResult + nSlots - nResults - 1));
                }
                nResults = nSlots;
                SetTop(firstResult + nSlots);
                allowPopRK = false; // PopRK is normally allowed here, but the reference implementation doesn't do it
            } else {
                allowPopRK = true;
            }
        }

        int ParseAssignmentWithResult() {
            ParseAssignment(out var allowPopRK);
            var result = allowPopRK ? PopRK() : PopS();
            SetTop(firstResult);
            return result;
        }

        bool TryParseCall(out int func, out int nArgs) {
            var token = Next();
            switch (token.type) {
                case TokenType.OpenParen: {
                    func = Head;
                    nArgs = Peek() == TokenType.CloseParen ? 0 : ParseArgs(0);
                    Expect(TokenType.CloseParen, "arguments");
                    return true;
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    func = Head;
                    PushK(ParseString(token.type == TokenType.SingleQuote ? '\'' : '"'));
                    nArgs = 1;
                    return true;
                }
                case TokenType.OpenBrace: {
                    func = Head;
                    PushTableConstructor();
                    nArgs = 1;
                    return true;
                }
                case TokenType.Colon: {
                    var src = PopRK();
                    var name = Expect(TokenType.Identifier, "self call")!;
                    Expect(TokenType.OpenParen, "self call");
                    func = Push();
                    code.Add(Build3(SELF, func, src, Constant(name)));
                    Push(); // push self
                    nArgs = Peek() == TokenType.CloseParen ? 1 : ParseArgs(1);
                    Expect(TokenType.CloseParen, "arguments");
                    return true;
                }
                default:
                    func = 0;
                    nArgs = 0;
                    lexer.PushBack(token);
                    return false;
            }
        }

        void ParseVarSuffix(int resultIdx) {

            switch (Peek()) {
                case TokenType.Dot: {
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    var constIdx = Constant(name);
                    if (PeekSuffix()) {
                        var src = PopRK();
                        code.Add(Build3(GETTABLE, Push(), src, constIdx));
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment()) {
                        var result = ParseAssignmentWithResult();
                        code.Add(Build3(SETTABLE, PopS(), constIdx, result));
                    } else if (PeekComma()) {
                        ParseVarAdditional(resultIdx);
                        code.Add(Build3(SETTABLE, PopS(), constIdx, firstResult + resultIdx));
                    } else {
                        throw new Exception($"Unexpected token {Peek()}");
                    }
                    break;
                }
                case TokenType.OpenBracket: {
                    Next();
                    var _src = TryPopRK();
                    PushExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    if (PeekSuffix()) {
                        var indexer = PopRK();
                        var src = _src ?? PopS();
                        code.Add(Build3(GETTABLE, Push(), src, indexer));
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment()) {
                        ParseAssignment(out var allowPopRK);
                        var result = allowPopRK ? PopRK() : PopS();
                        SetTop(firstResult);
                        var indexer = PopRK();
                        code.Add(Build3(SETTABLE, PopS(), indexer, result));
                    } else if (PeekComma()) {
                        ParseVarAdditional(resultIdx);
                        var indexer = PopRK();
                        code.Add(Build3(SETTABLE, PopS(), indexer, firstResult + resultIdx));
                    } else {
                        throw new Exception($"Unexpected token {Peek()}");
                    }
                    break;
                }
                default: {
                    Debug.Assert(TryParseCall(out var func, out var nArgs));
                    if (PeekSuffix()) {
                        code.Add(Build3(CALL, func, nArgs + 1, 2));
                        SetTop(func + 1);
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment() || PeekComma() || nSlots > 1) {
                        throw new Exception("Cannot assign to function call");
                    } else {
                        code.Add(Build3(CALL, func, nArgs + 1, 1));
                        SetTop(func);
                    }
                    break;
                }
            }
        }

        void PushExpressionSuffix() {

            switch (Peek()) {
                case TokenType.Dot: {
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    var indexer = Constant(name);
                    var src = PopRK();
                    code.Add(Build3(GETTABLE, Push(), src, indexer));
                    PushExpressionSuffix();
                    break;
                }
                case TokenType.OpenBracket: {
                    Next();
                    var _src = TryPopRK();
                    PushExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    var indexer = PopRK();
                    var src = _src ?? PopS();
                    code.Add(Build3(GETTABLE, Push(), src, indexer));
                    PushExpressionSuffix();
                    break;
                }
                default:
                    if (TryParseCall(out var func, out var nArgs)) {
                        code.Add(Build3(CALL, func, nArgs + 1, 2));
                        SetTop(func + 1);
                        PushExpressionSuffix();
                    }
                    break;
            }
        }

        void PushExpression() {
            PushOperationSequence();
        }

        OpCode? GetBOp(out bool invert, out int order) {
            invert = false;
            order = -1;
            switch (Peek()) {
                case TokenType.Or: order = 0; return TEST;
                case TokenType.And: order = 1; return TEST;
                case TokenType.LessThan: order = 2; return LT;
                case TokenType.GreaterThan: order = 2; invert = true; return LT;
                case TokenType.LessThanEqual: order = 2; return LE;
                case TokenType.GreaterThanEqual: order = 2; order = 2; invert = true; return LE;
                case TokenType.DoubleEqual: order = 2; return EQ;
                case TokenType.NotEqual: order = 2; invert = true; return EQ;
                case TokenType.Pipe: order = 3; return BOR;
                case TokenType.Tilde: order = 4; return BXOR;
                case TokenType.Ampersand: order = 5; return BAND;
                case TokenType.ShiftLeft: order = 6; return SHL;
                case TokenType.ShiftRight: order = 6; return SHR;
                case TokenType.DoubleDot: order = 7; return CONCAT;
                case TokenType.Plus: order = 8; return ADD;
                case TokenType.Minus: order = 8; return SUB;
                case TokenType.Star: order = 9; return MUL;
                case TokenType.Slash: order = 9; return DIV;
                case TokenType.DoubleSlash: order = 9; return IDIV;
                case TokenType.Percent: order = 9; return MOD;
                case TokenType.Caret: order = 11; return POW;
                default: return null;
            }
        }

        OpCode? GetUOp(out int order) {
            order = -1;
            switch (Peek()) {
                case TokenType.Not: order = 10; return NOT;
                case TokenType.Hash: order = 10; return LEN;
                case TokenType.Minus: order = 10; return UNM;
                case TokenType.Tilde: order = 10; return BNOT;
                default: return null;
            }
        }

        struct Operation {
            public OpCode opcode;
            public int order;
            public bool invert;
            public bool isUnary;
        }

        readonly Stack<Operation> ops = new Stack<Operation>();
        readonly Stack<int?> operands = new Stack<int?>();

        void PushOperationSequence() {
            void Resolve(int order) {
                while (ops.Count > 0 && ops.Peek().order >= order) {
                    var op = ops.Pop();
                    if (op.isUnary) {
                        var left = operands.Pop() ?? PopS();
                        code.Add(Build2(op.opcode, Push(), left));
                        operands.Push(null);
                    } else {
                        var right = operands.Pop() ?? PopS();
                        var left = operands.Pop() ?? PopS();
                        code.Add(Build3(op.opcode, Push(), left, right));
                        operands.Push(null);
                    }
                }
            }

            bool wasPushed = false;

            while (true) {
                var uop = GetUOp(out var order);
                if (uop != null) {
                    Next();
                }
                PushTerm();
                if (uop != null) {
                    operands.Push(TryPopRK());
                    wasPushed = true;
                    Resolve(order);
                    ops.Push(new Operation {
                        opcode = uop.Value,
                        order = order,
                        invert = false,
                        isUnary = true
                    });
                } else {
                    wasPushed = false;
                }
                var bop = GetBOp(out var invert, out order);
                if (bop == null) break;
                Next();
                if (!wasPushed) {
                    operands.Push(TryPopRK());
                    wasPushed = true;
                }
                Resolve(order);
                ops.Push(new Operation {
                    opcode = bop.Value,
                    order = order,
                    invert = invert,
                    isUnary = false
                });
            }
            if (ops.Count > 0) {
                if (!wasPushed) {
                    operands.Push(TryPopRK());
                }
                Resolve(-1);
            }
            Debug.Assert(ops.Count == 0);
            Debug.Assert(operands.Count <= 1);
            if (operands.Count == 1) {
                var result = operands.Pop();
                if (result != null) {
                    AdjustLHS(result.Value, Push());
                }
            }
        }

        Compiler(Compiler parent) {
            this.parent = parent;
            lexer = parent.lexer;
        }

        void PushFunction(bool hasSelf) {
            Expect(TokenType.OpenParen, "function");
            var scope = new Compiler(this);
            if (hasSelf) {
                scope.locals.Add("self");
            }
            while (Peek() != TokenType.CloseParen) {
                scope.locals.Add(Expect(TokenType.Identifier, "function argument")!);
                if (Peek() == TokenType.Comma) {
                    Next();
                } else if (Peek() != TokenType.CloseParen) {
                    throw new Exception($"Expected ',' or ')' but got {Peek()}");
                }
            }
            Next();
            scope.SetTop(scope.locals.Count);
            scope.ParseBlock();
            code.Add(Build2(CLOSURE, Push(), prototypes.Count));
            prototypes.Add(scope.MakeFunction());
        }

        void PushTerm() {
            // void Unary(OpCode op) {
            //     PushTerm();
            //     var expr = PopS();
            //     code.Add(Build2(op, Push(), expr));
            // }

            void Literal(OpCode op, int arg) {
                code.Add(Build2(op, Push(), arg));
            }

            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var localIdx = locals.IndexOf(name);
                    if (localIdx >= 0) {
                        code.Add(Build2(MOVE, Push(), localIdx));
                    } else {
                        var globalIdx = Constant(name);
                        code.Add(Build3(GETTABUP, Push(), EnvUp(), globalIdx));
                    }
                    PushExpressionSuffix();
                    break;
                }
                case TokenType.Number: PushK(token.number); break;
                case TokenType.DoubleQuote: PushK(ParseString('"')); break;
                case TokenType.SingleQuote: PushK(ParseString('\'')); break;
                case TokenType.OpenParen: {
                    PushExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    PushExpressionSuffix();
                    break;
                }
                // case TokenType.Hash: Unary(LEN); break;
                // case TokenType.Not: Unary(NOT); break;
                // case TokenType.Tilde: Unary(BNOT); break;
                // case TokenType.Minus: Unary(UNM); break;
                case TokenType.True: Literal(LOADBOOL, 1); break;
                case TokenType.False: Literal(LOADBOOL, 0); break;
                case TokenType.Nil: Literal(LOADNIL, 0); break;
                case TokenType.OpenBrace: PushTableConstructor(); break;
                case TokenType.TripleDot:
                    code.Add(Build2(VARARG, Push(), 2));
                    break;
                case TokenType.Function: PushFunction(hasSelf: false); break;
                default:
                    throw new Exception($"Unexpected token {token.type}");
            }
        }

        void PushConcatSequence() {
            var start = Top;
            do {
                PushTerm();
            } while (TryTake(TokenType.DoubleDot));
            var end = Head;
            if (start != end) {
                SetTop(start);
                code.Add(Build3(CONCAT, Push(), start, end));
            }
        }

        void PushTableConstructor() {
            var opIdx = code.Count;
            var table = Push();
            code.Add(0);
            int nArr = 0;
            int nHash = 0;
            while (Peek() != TokenType.CloseBrace) {
                switch (Peek()) {
                    case TokenType.Identifier: {
                        var key = Next();
                        if (Peek() == TokenType.Equal) {
                            Next();
                            var constIdx = Constant(key.text!);
                            PushExpression();
                            code.Add(Build3(SETTABLE, table, constIdx, PopRK()));
                            nHash++;
                        } else {
                            lexer.PushBack(key);
                            PushExpression();
                            nArr++;
                        }
                        break;
                    }
                    case TokenType.OpenBracket: {
                        Next();
                        PushExpression();
                        Expect(TokenType.CloseBracket, "table index expression");
                        Expect(TokenType.Equal, "table index");
                        PushExpression();
                        var value = PopRK();
                        code.Add(Build3(SETTABLE, table, PopRK(), value));
                        nHash++;
                        break;
                    }
                    default:
                        PushExpression();
                        nArr++;
                        break;
                }
                switch (Peek()) {
                    case TokenType.Comma:
                        Next();
                        break;
                    case TokenType.CloseBrace:
                        break;
                    default:
                        throw new Exception("'}' or ',' expected");
                }
            }
            Next();
            if (nArr > 0) {
                if (ExtendVararg(0)) {
                    nArr = 0;
                }
                code.Add(Build3(SETLIST, table, nArr, 1));
            }
            code[opIdx] = Build3(NEWTABLE, table, nArr, nHash);
            SetTop(table + 1);
        }

        void ParseVarAdditional(int resultIdx) {
            ParseVar(resultIdx + 1);
        }

        void AdjustLHS(int result, int dst) {
            if (result == dst) {
                return;
            }
            if ((result & KFlag) != 0) {
                code.Add(Build2x(LOADK, dst, result & ~KFlag));
                return;
            }
            var inst = code[code.Count - 1];
            switch (GetOpCode(inst)) {
                case ADD:
                case SUB:
                case MUL:
                case DIV:
                case MOD:
                case POW:
                case BAND:
                case BOR:
                case BXOR:
                case SHL:
                case SHR:
                case UNM:
                case BNOT:
                case NOT:
                case CONCAT:
                case GETTABLE:
                case GETTABUP:
                case GETUPVAL:
                case NEWTABLE:
                case LEN:
                case CLOSURE:
                case LOADK:
                case MOVE:
                    var a = GetA(inst);
                    if (a == result) {
                        Shrink();
                        code.Add(Build2x(GetOpCode(inst), dst, GetBx(inst)));
                    }
                    break;
                default:
                    code.Add(Build2(MOVE, dst, result));
                    break;
            }
        }

        void ParseVar(int resultIdx) {
            nSlots++;
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var localIdx = locals.IndexOf(name);
                    if (localIdx < 0) {
                        var constIdx = Constant(name);
                        if (PeekSuffix()) {
                            code.Add(Build3(GETTABUP, Push(), EnvUp(), constIdx));
                            ParseVarSuffix(resultIdx);
                        } else if (PeekAssignment()) {
                            var result = ParseAssignmentWithResult();
                            code.Add(Build3(SETTABUP, EnvUp(), constIdx, result));
                        } else if (PeekComma()) {
                            ParseVarAdditional(resultIdx);
                            code.Add(Build3(SETTABUP, EnvUp(), constIdx, firstResult + resultIdx));
                        } else {
                            throw new Exception("Expected a call or assignment");
                        }
                    } else {
                        if (PeekSuffix()) {
                            code.Add(Build2(MOVE, Push(), localIdx));
                            ParseVarSuffix(resultIdx);
                        } else if (PeekAssignment()) {
                            ParseAssignment(out var _);
                            AdjustLHS(Head, localIdx);
                            SetTop(firstResult);
                        } else if (PeekComma()) {
                            ParseVarAdditional(resultIdx);
                            code.Add(Build2(MOVE, localIdx, firstResult + resultIdx));
                        } else {
                            throw new Exception("Expected a call or assignment");
                        }
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    PushExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    if (PeekSuffix()) {
                        ParseVarSuffix(resultIdx);
                    } else if (PeekAssignment() || PeekComma()) {
                        throw new Exception("Cannot assign to parenthesized expression");
                    } else {
                        throw new Exception("Expected an call or assignment");
                    }
                    break;
                }
                default:
                    throw new Exception($"Unexpected token {token.type}");
            }
        }

        void ParseBlock() {
            while (true) {
                switch (Peek()) {
                    case TokenType.End:
                        Next();
                        return;
                    default:
                        ParseStat();
                        break;
                }
            }
        }

        private readonly List<string> tmpLocals = new List<string>();

        void ParseLocal() {
            Next();
            if (Peek() == TokenType.Function) {
                Next();
                var name = Expect(TokenType.Identifier, "function name")!;
                PushFunction(hasSelf: false);
                locals.Add(name);
                return;
            }
            do {
                tmpLocals.Add(Expect(TokenType.Identifier, "local declaration")!);
            } while (TryTake(TokenType.Comma));
            nSlots = tmpLocals.Count;
            if (Peek() == TokenType.Equal) {
                Next();
                ParseAssignment(out var _);
            } else {
                code.Add(Build2(LOADNIL, Top, Top + tmpLocals.Count - 1));
            }
            SetTop(firstResult + tmpLocals.Count);
            for (var i = 0; i < tmpLocals.Count; i++) {
                locals.Add(tmpLocals[i]);
            }
            tmpLocals.Clear();
        }
    }
}

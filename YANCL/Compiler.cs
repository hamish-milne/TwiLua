using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static YANCL.Instruction;
using static YANCL.OpCode;

namespace YANCL
{
    enum TokenType {
        Eof,
        Identifier,
        Number,
        SingleQuote,
        DoubleQuote,
        OpenParen,
        CloseParen,
        OpenBracket,
        CloseBracket,
        OpenBrace,
        CloseBrace,
        Comma,
        Dot,
        DoubleDot,
        TripleDot,
        Colon,
        Semicolon,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,
        Caret,
        Tilde,
        Equal,
        DoubleEqual,
        NotEqual,
        LessThan,
        GreaterThan,
        LessThanEqual,
        GreaterThanEqual,
        Hash,

        // Keywords
        And,
        Or,
        Not,
        If,
        Then,
        Else,
        ElseIf,
        For,
        Do,
        While,
        End,
        Return,
        Local,
        Function,
        True,
        False,
        Nil,
        Break,
        Repeat,
        Until,
        In,
    }

    public class Compiler
    {
        int position;
        string str;

        public static LuaFunction Compile(string str) {
            var c = new Compiler {
                str = str,
            };
            c.ParseChunk();
            return new LuaFunction {
                code = c.code.ToArray(),
                constants = c.constants.ToArray(),
                upvalues = Array.Empty<LuaUpValue>(),
                prototypes = Array.Empty<LuaFunction>(),
                nLocals = c.stack.Count,
                nSlots = c.maxStack - c.stack.Count,
            };
        }

        readonly List<int> code = new List<int>(1024);

        struct Token {
            public TokenType type;
            public string? text;
            public double number;

            public static implicit operator Token(TokenType type) => new Token { type = type };
        }

        Stack<Token> tokens = new Stack<Token>();

        Token Next() {
            if (tokens.Count > 0) {
                return tokens.Pop();
            }

            if (position >= str.Length) {
                return TokenType.Eof;
            }

            var start = position;
            char c = str[position++];
            if (char.IsWhiteSpace(c)) {
                while (position < str.Length && char.IsWhiteSpace(str[position])) {
                    position++;
                }
                return Next();
            }

            if (char.IsLetter(c) || c == '_') {
                while (position < str.Length && (char.IsLetter(str[position]) || char.IsDigit(str[position]) || str[position] == '_')) {
                    position++;
                }
                var identifier = str.Substring(start, position - start);
                switch (identifier) {
                    case "and": return TokenType.And;
                    case "or": return TokenType.Or;
                    case "not": return TokenType.Not;
                    case "if": return TokenType.If;
                    case "then": return TokenType.Then;
                    case "else": return TokenType.Else;
                    case "elseif": return TokenType.ElseIf;
                    case "for": return TokenType.For;
                    case "do": return TokenType.Do;
                    case "while": return TokenType.While;
                    case "end": return TokenType.End;
                    case "return": return TokenType.Return;
                    case "local": return TokenType.Local;
                    case "function": return TokenType.Function;
                    case "true": return TokenType.True;
                    case "false": return TokenType.False;
                    case "nil": return TokenType.Nil;
                    case "break": return TokenType.Break;
                    case "repeat": return TokenType.Repeat;
                    case "until": return TokenType.Until;
                    case "in": return TokenType.In;
                    default: return new Token {
                        type = TokenType.Identifier,
                        text = identifier
                    };
                }
            }

            if (char.IsDigit(c) || (c == '.' && position < str.Length && char.IsDigit(str[position]))) {
                position--;
                bool hasDp = false;
                bool end = false;
                bool hasMantissa = false;
                bool negativeExponent = false;
                long mantissa = 0;
                int exponent = 0;
                while (!end && position < str.Length) {
                    switch (str[position]) {
                        case '.':
                            if (hasDp || hasMantissa) {
                                end = true;
                                break;
                            }
                            hasDp = true;
                            position++;
                            break;
                        case 'e':
                        case 'E':
                            if (hasMantissa) {
                                end = true;
                                break;
                            }
                            hasMantissa = true;
                            position++;
                            if (position < str.Length) {
                                switch (str[position]) {
                                    case '-':
                                        negativeExponent = true;
                                        position++;
                                        break;
                                    case '+':
                                        position++;
                                        break;
                                }
                            }
                            break;
                        case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
                            if (hasMantissa) {
                                exponent = exponent * 10 + ((str[position] - '0') * (negativeExponent ? -1 : 1));
                                position++;
                            } else {
                                mantissa = mantissa * 10 + (str[position] - '0');
                                if (hasDp) {
                                    exponent -= 1;
                                }
                                position++;
                            }
                            break;
                        default:
                            end = true;
                            break;
                    }
                }
                return new Token {
                    type = TokenType.Number,
                    number = mantissa * Math.Pow(10, exponent)
                };
            }

            TokenType TwoCharToken(TokenType token, char c, TokenType token2) {
                if (position < str.Length && str[position] == c) {
                    position++;
                    return token2;
                }
                return token;
            }


            switch (c) {
                case '\'': return TokenType.SingleQuote;
                case '"': return TokenType.DoubleQuote;
                case '(': return TokenType.OpenParen;
                case ')': return TokenType.CloseParen;
                case '[': return TokenType.OpenBracket;
                case ']': return TokenType.CloseBracket;
                case '{': return TokenType.OpenBrace;
                case '}': return TokenType.CloseBrace;
                case ',': return TokenType.Comma;
                case '.':
                    if (position < str.Length && str[position] == '.') {
                        position++;
                        if (position < str.Length && str[position] == '.') {
                            position++;
                            return TokenType.TripleDot;
                        }
                        return TokenType.DoubleDot;
                    }
                    return TokenType.Dot;
                case ':': return TokenType.Colon;
                case ';': return TokenType.Semicolon;
                case '+': return TokenType.Plus;
                case '-': return TokenType.Minus;
                case '*': return TokenType.Star;
                case '/': return TokenType.Slash;
                case '%': return TokenType.Percent;
                case '^': return TokenType.Caret;
                case '~': return TwoCharToken(TokenType.Tilde, '=', TokenType.NotEqual);
                case '=': return TwoCharToken(TokenType.Equal, '=', TokenType.DoubleEqual);
                case '<': return TwoCharToken(TokenType.LessThan, '=', TokenType.LessThanEqual);
                case '>': return TwoCharToken(TokenType.GreaterThan, '=', TokenType.GreaterThanEqual);
                case '#': return TokenType.Hash;
                default:
                    throw new Exception($"Unexpected character '{c}' at position {position}");
            }
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

        string? Expect(TokenType type, string after) {
            var token = Next();
            if (token.type != type) {
                throw new Exception($"Expected {type} after {after}, but got {token.type}");
            }
            return token.text;
        }

        void ParseStat() {
            switch (Peek()) {
                case TokenType.Identifier:
                case TokenType.OpenParen:
                case TokenType.OpenBrace:
                case TokenType.Minus:
                    ParseVar(new ParseState {
                        compiler = this,
                    });
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
                    throw new NotImplementedException();
                case TokenType.Return:
                    throw new NotImplementedException();
                case TokenType.Semicolon:
                    Next();
                    break;
                default:
                    throw new Exception($"Unexpected token {Peek()}");
            }
        }


        public readonly List<LuaValue> constants = new List<LuaValue>();

        int Constant(string str) {
            var constIdx = constants.IndexOf(str);
            if (constIdx < 0) {
                constIdx = constants.Count;
                constants.Add(str);
            }
            return constIdx | KFlag;
        }

        int Constant(double num) {
            var constIdx = constants.IndexOf(num);
            if (constIdx < 0) {
                constIdx = constants.Count;
                constants.Add(num);
            }
            return constIdx | KFlag;
        }

        string ParseString(char term) {
            var sb = new StringBuilder();
            while (true) {
                var c = str[position++];
                if (c == '\\') {
                    c = str[position++];
                    switch (c) {
                        case 'a': sb.Append('\a'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'v': sb.Append('\v'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        case '\n': break;
                        default:
                            throw new Exception($"Invalid escape sequence '\\{c}'");
                    }
                } else if (c == term) {
                    break;
                } else {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        TokenType Peek() {
            if (tokens.Count == 0) {
                tokens.Push(Next());
            }
            return tokens.Peek().type;
        }

        
        readonly List<string?> stack = new List<string?>();
        int maxStack;

        public void SetTop(int top) {
            while (stack.Count < top) {
                stack.Add(null);
            }
            maxStack = Math.Max(maxStack, stack.Count);
            while (stack.Count > top) {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        class ParseState {
            public Compiler compiler;
            public int nSlots;
            public int nResults;
            public int firstResult;
            public int lastValue;

            public int TopResult => firstResult + nResults - 1;

            public bool IsTemporary(int idx) {
                return compiler.stack[idx] == null;
            }

            public int Push() {
                compiler.stack.Add(null);
                compiler.maxStack = Math.Max(compiler.maxStack, compiler.stack.Count);
                return compiler.stack.Count - 1;
            }

            public void Pop(int idx) {
                if ((idx & KFlag) != 0) {
                    return;
                }
                Debug.Assert(idx == compiler.stack.Count - 1);
                if (compiler.stack[idx] != null) {
                    return;
                }
                compiler.stack.RemoveAt(idx);
            }

            public int? GetLocal(string name) {
                var index = compiler.stack.IndexOf(name);
                if (index == -1) {
                    return null;
                } else {
                    return index;
                }
            }

            public int PushLocal(string name) {
                var index = compiler.stack.IndexOf(name);
                if (index == -1) {
                    Debug.Assert(compiler.stack.Count == 0 || compiler.stack[compiler.stack.Count - 1] != null);
                    compiler.stack.Add(name);
                    return compiler.stack.Count - 1;
                } else {
                    throw new Exception($"Local {name} already defined");
                }
            }

            public int EnvUp() {
                return 0;
            }
        }

        bool PeekSuffix() {
            switch (Peek()) {
                case TokenType.Dot:
                case TokenType.OpenBracket:
                case TokenType.OpenParen:
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote:
                    return true;
                default:
                    return false;
            }
        }

        bool PeekAssignment() {
            if (Peek() == TokenType.Equal) {
                Next();
                return true;
            }
            return false;
        }

        bool PeekComma() {
            if (Peek() == TokenType.Comma) {
                Next();
                return true;
            }
            return false;
        }

        void AdjustDestination(ParseState state, int old, int newIdx) {
            if (old == newIdx) {
                return;
            }
            if ((old & KFlag) != 0) {
                var constIdx = old & ~KFlag;
                code.Add(Build2x(LOADK, newIdx, constIdx));
            } else {
                code.Add(Build2(MOVE, newIdx, old));
                state.Pop(old);
                state.Pop(newIdx);
            }
        }

        int PreCall(ParseState state, int src) {
            var func = src;
            if (!state.IsTemporary(src)) {
                func = state.Push();
                code.Add(Build2(MOVE, func, src));
            }
            return func;
        }

        int ParseArgs(ParseState state, int func) {
            var nArgs = 0;
            while (Peek() != TokenType.CloseParen) {
                nArgs++;
                var arg = ParseExpression(state);
                SetTop(func + 1 + nArgs);
                AdjustDestination(state, arg, func + nArgs);
                if (Peek() == TokenType.Comma) {
                    Next();
                } else if (Peek() != TokenType.CloseParen) {
                    throw new Exception("Expected ',' or ')' but got " + Peek());
                }
            }
            Next();
            return nArgs;
        }

        void ExtendMultiReturn(ParseState state) {
            var inst = code[code.Count - 1];
            switch (Instruction.GetOpCode(inst)) {
                case CALL: {
                    var a = Instruction.GetA(inst);
                    var b = Instruction.GetB(inst);
                    code[code.Count - 1] = Build3(CALL, a, b, state.nSlots - state.nResults + 2);
                    state.nResults = state.nSlots;
                    break;
                }
                case VARARG: {
                    var a = Instruction.GetA(inst);
                    code[code.Count - 1] = Build2(VARARG, a, state.nSlots - state.nResults + 2);
                    state.nResults = state.nSlots;
                    break;
                }
            }
        }

        void ParseAssignment(ParseState state) {
            var baseR = stack.Count;
            state.firstResult = baseR;
            state.nResults = 0;
            while (true) {
                state.nResults++;
                state.lastValue = ParseExpression(state);
                if (Peek() == TokenType.Comma) {
                    SetTop(baseR + state.nResults);
                    AdjustDestination(state, state.lastValue, baseR + state.nResults - 1);
                    Next();
                } else {
                    break;
                }
            }
            if (state.nResults != state.nSlots) {
                SetTop(baseR + state.nResults);
                AdjustDestination(state, state.lastValue, baseR + state.nResults - 1);
                if (state.nResults < state.nSlots) {
                    ExtendMultiReturn(state);
                }
                if (state.nResults < state.nSlots) {
                    code.Add(Build2(LOADNIL, baseR + state.nResults, baseR + state.nSlots - state.nResults - 1));
                }
                state.lastValue = baseR + state.nSlots - 1;
                SetTop(baseR + state.nSlots);
                state.nResults = state.nSlots;
            }
        }

        void PopAssignment(ParseState state) {
            Debug.Assert(state.nResults > 0);
            if (state.nResults == state.nSlots && state.lastValue != state.TopResult) {
                state.nResults--;
                return; // Last value is not in the stack
            }
            state.Pop(state.firstResult + state.nResults - 1);
            state.nResults--;
        }
            

        void ParseVarSuffix(ParseState state, int src) {

            void PostCall(int func, int nArgs) {
                if (PeekSuffix()) {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 1);
                    ParseVarSuffix(state, src);
                } else if (PeekAssignment() || PeekComma() || state.nSlots > 1) {
                    throw new Exception("Cannot assign to function call");
                } else {
                    code.Add(Build3(CALL, func, nArgs + 1, 1));
                    SetTop(func);
                }
            }

            var token = Next();
            switch (token.type) {
                case TokenType.Dot: {
                    var name = Expect(TokenType.Identifier, "dot")!;
                    var constIdx = Constant(name);
                    if (PeekSuffix()) {
                        if (state.IsTemporary(src)) {
                            code.Add(Build3(GETTABLE, src, src, constIdx));
                            ParseVarSuffix(state, src);
                        } else {
                            var dst = state.Push();
                            code.Add(Build3(GETTABLE, dst, src, constIdx));
                            ParseVarSuffix(state, dst);
                        }
                    } else if (PeekAssignment()) {
                        ParseAssignment(state);
                        code.Add(Build3(SETTABLE, src, constIdx, state.lastValue));
                        PopAssignment(state);
                        state.Pop(src);
                    } else if (PeekComma()) {
                        ParseVarAdditional(state);
                        code.Add(Build3(SETTABLE, src, constIdx, state.TopResult));
                        PopAssignment(state);
                        state.Pop(src);
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenBracket: {
                    var indexer = ParseExpression(state);
                    Expect(TokenType.CloseBracket, "index expression");
                    if (PeekSuffix()) {
                        if (state.IsTemporary(src)) {
                            code.Add(Build3(GETTABLE, src, src, indexer));
                            ParseVarSuffix(state, src);
                        } else {
                            var dst = state.Push();
                            code.Add(Build3(GETTABLE, dst, src, indexer));
                            ParseVarSuffix(state, dst);
                        }
                    } else if (PeekAssignment()) {
                        ParseAssignment(state);
                        code.Add(Build3(SETTABLE, src, indexer, state.lastValue));
                        PopAssignment(state);
                        state.Pop(indexer);
                        state.Pop(src);
                    } else if (PeekComma()) {
                        ParseVarAdditional(state);
                        code.Add(Build3(SETTABLE, src, indexer, state.TopResult));
                        PopAssignment(state);
                        state.Pop(indexer);
                        state.Pop(src);
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    var func = PreCall(state, src);
                    var nArgs = ParseArgs(state, func);
                    PostCall(func, nArgs);
                    break;
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    var func = PreCall(state, src);
                    var arg = Constant(ParseString(token.type == TokenType.SingleQuote ? '\'' : '"'));
                    SetTop(func + 2);
                    AdjustDestination(state, arg, func + 1);
                    PostCall(func, 1);
                    break;
                }
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        int ParseExpressionSuffix(ParseState state, int src) {

            int PostCall(int func, int nArgs) {
                int ret;
                if (PeekSuffix()) {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 2);
                    ret = ParseExpressionSuffix(state, func);
                } else {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 1);
                    ret = func;
                }
                return ret;
            }

            switch (Peek()) {
                case TokenType.Dot: {
                    Next();
                    var name = Expect(TokenType.Identifier, "dot")!;
                    var indexer = Constant(name);
                    var dst = state.IsTemporary(src) ? src : state.Push();
                    code.Add(Build3(GETTABLE, dst, src, indexer));
                    return ParseExpressionSuffix(state, dst);
                }
                case TokenType.OpenBracket: {
                    Next();
                    var indexer = ParseExpression(state);
                    Expect(TokenType.CloseBracket, "index expression");
                    var dst = state.IsTemporary(src) ? src : state.Push();
                    code.Add(Build3(GETTABLE, dst, src, indexer));
                    return ParseExpressionSuffix(state, dst);
                }
                case TokenType.OpenParen: {
                    Next();
                    var func = PreCall(state, src);
                    var nArgs = ParseArgs(state, func);
                    return PostCall(func, nArgs);
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    Next();
                    var func = PreCall(state, src);
                    var arg = Constant(ParseString(Peek() == TokenType.SingleQuote ? '\'' : '"'));
                    SetTop(func + 2);
                    AdjustDestination(state, arg, func + 1);
                    return PostCall(func, 1);
                }
                default:
                    return src;
            }
        }

        int ParseExpression(ParseState state) {
            return ParseTerm(state);
        }

        int ParseTerm(ParseState state) {
            int Unary(OpCode op) {
                var expr = ParseTerm(state);
                var idx = state.Push();
                code.Add(Build2(op, idx, expr));
                return idx;
            }

            int Literal(OpCode op, int arg) {
                var idx = state.Push();
                code.Add(Build2(op, idx, arg));
                return idx;
            }

            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var localIdx = state.GetLocal(name);
                    if (localIdx != null) {
                        return ParseExpressionSuffix(state, localIdx.Value);
                    } else {
                        var globalIdx = Constant(name);
                        var idx = state.Push();
                        code.Add(Build3(GETTABUP, idx, state.EnvUp(), globalIdx));
                        return ParseExpressionSuffix(state, idx);
                    }
                }
                case TokenType.Number: return Constant(token.number);
                case TokenType.DoubleQuote: return Constant(ParseString('"'));
                case TokenType.SingleQuote: return Constant(ParseString('\''));
                case TokenType.OpenParen: {
                    var expr = ParseExpression(state);
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    return ParseExpressionSuffix(state, expr);
                }
                case TokenType.Hash: return Unary(LEN);
                case TokenType.Not: return Unary(NOT);
                case TokenType.Minus: return Unary(UNM);
                case TokenType.True: return Literal(LOADBOOL, 1);
                case TokenType.False: return Literal(LOADBOOL, 0);
                case TokenType.Nil: return Literal(LOADNIL, 0);
                default:
                    throw new Exception($"Unexpected token {token.type}");
            }
        }

        void ParseVarAdditional(ParseState state) {
            ParseVar(state);
        }

        void ParseVar(ParseState state) {
            
            state.nSlots++;
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var index = state.GetLocal(name);
                    if (index == null) {
                        var constIdx = Constant(name);
                        if (PeekSuffix()) {
                            var dst = state.Push();
                            code.Add(Build3(GETTABUP, dst, state.EnvUp(), constIdx));
                            ParseVarSuffix(state, dst);
                        } else if (PeekAssignment()) {
                            ParseAssignment(state);
                            code.Add(Build3(SETTABUP, state.EnvUp(), constIdx, state.lastValue));
                            PopAssignment(state);
                        } else if (PeekComma()) {
                            ParseVarAdditional(state);
                            code.Add(Build3(SETTABUP, state.EnvUp(), constIdx, state.TopResult));
                            PopAssignment(state);
                        } else {
                            throw new Exception("Expected an call or assignment");
                        }
                    } else {
                        if (PeekSuffix()) {
                            ParseVarSuffix(state, index.Value);
                        } else if (PeekAssignment()) {
                            var result = ParseExpression(state);
                            AdjustDestination(state, result, index.Value);
                        } else if (PeekComma()) {
                            ParseVarAdditional(state);
                            AdjustDestination(state, state.TopResult, index.Value);
                        } else {
                            throw new Exception("Expected an call or assignment");
                        }
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    var result = ParseExpression(state);
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    if (PeekSuffix()) {
                        ParseVarSuffix(state, result);
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

        void ParseLocal() {
            var names = new List<string>();
            do {
                Next();
                names.Add(Expect(TokenType.Identifier, "local declaration")!);
            } while (Peek() == TokenType.Comma);
            int startR = stack.Count;
            if (Peek() == TokenType.Equal) {
                Next();
                var state = new ParseState {
                    compiler = this,
                    nSlots = names.Count,
                };
                ParseAssignment(state);
                if (state.lastValue != state.TopResult) {
                    AdjustDestination(state, state.lastValue, state.Push());
                }
                Debug.Assert(state.firstResult == startR);
                Debug.Assert(stack.Count == startR + names.Count);
            } else {
                code.Add(Build2(LOADNIL, stack.Count, stack.Count + names.Count - 1));
                SetTop(startR + names.Count);
            }
            for (var i = 0; i < names.Count; i++) {
                stack[startR + i] = names[i];
            }
        }
    }
}

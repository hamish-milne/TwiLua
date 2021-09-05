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

        Stack<(TokenType, string?)> tokens = new Stack<(TokenType, string?)>();

        (TokenType type, string? text) Next() {
            if (tokens.Count > 0) {
                return tokens.Pop();
            }

            if (position >= str.Length) {
                return (TokenType.Eof, null);
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
                    case "and": return (TokenType.And, null);
                    case "or": return (TokenType.Or, null);
                    case "not": return (TokenType.Not, null);
                    case "if": return (TokenType.If, null);
                    case "then": return (TokenType.Then, null);
                    case "else": return (TokenType.Else, null);
                    case "elseif": return (TokenType.ElseIf, null);
                    case "for": return (TokenType.For, null);
                    case "do": return (TokenType.Do, null);
                    case "while": return (TokenType.While, null);
                    case "end": return (TokenType.End, null);
                    case "return": return (TokenType.Return, null);
                    case "local": return (TokenType.Local, null);
                    case "function": return (TokenType.Function, null);
                    case "true": return (TokenType.True, null);
                    case "false": return (TokenType.False, null);
                    case "nil": return (TokenType.Nil, null);
                    case "break": return (TokenType.Break, null);
                    case "repeat": return (TokenType.Repeat, null);
                    case "until": return (TokenType.Until, null);
                    case "in": return (TokenType.In, null);
                    default: return (TokenType.Identifier, identifier);
                }
            }

            if (char.IsDigit(c)) {
                while (position < str.Length && char.IsDigit(str[position])) {
                    position++;
                }
                return (TokenType.Number, str.Substring(start, position - start));
            }

            (TokenType, string?) CharToken(TokenType token) {
                return (token, null);
            }

            (TokenType, string?) TwoCharToken(TokenType token, char c, TokenType token2) {
                if (position < str.Length && str[position] == c) {
                    position++;
                    return (token2, null);
                }
                return (token, null);
            }


            switch (c) {
                case '\'': return CharToken(TokenType.SingleQuote);
                case '"': return CharToken(TokenType.DoubleQuote);
                case '(': return CharToken(TokenType.OpenParen);
                case ')': return CharToken(TokenType.CloseParen);
                case '[': return CharToken(TokenType.OpenBracket);
                case ']': return CharToken(TokenType.CloseBracket);
                case '{': return CharToken(TokenType.OpenBrace);
                case '}': return CharToken(TokenType.CloseBrace);
                case ',': return CharToken(TokenType.Comma);
                case '.':
                    if (position < str.Length && str[position] == '.') {
                        position++;
                        if (position < str.Length && str[position] == '.') {
                            position++;
                            return (TokenType.TripleDot, null);
                        }
                        return (TokenType.DoubleDot, null);
                    }
                    return (TokenType.Dot, null);
                case ':': return CharToken(TokenType.Colon);
                case ';': return CharToken(TokenType.Semicolon);
                case '+': return CharToken(TokenType.Plus);
                case '-': return CharToken(TokenType.Minus);
                case '*': return CharToken(TokenType.Star);
                case '/': return CharToken(TokenType.Slash);
                case '%': return CharToken(TokenType.Percent);
                case '^': return CharToken(TokenType.Caret);
                case '~': return TwoCharToken(TokenType.Tilde, '=', TokenType.NotEqual);
                case '=': return TwoCharToken(TokenType.Equal, '=', TokenType.DoubleEqual);
                case '<': return TwoCharToken(TokenType.LessThan, '=', TokenType.LessThanEqual);
                case '>': return TwoCharToken(TokenType.GreaterThan, '=', TokenType.GreaterThanEqual);
                case '#': return CharToken(TokenType.Hash);
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
                        slots = new List<int>()
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
                    throw new NotImplementedException();
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
            return tokens.Peek().Item1;
        }

        bool PeekHasSuffix() {
            switch (Peek()) {
                case TokenType.Dot:
                case TokenType.OpenBracket:
                case TokenType.OpenParen:
                    return true;
                default:
                    return false;
            }
        }

        
        readonly List<string?> stack = new List<string?>();
        int maxStack;

        struct ParseState {
            public Compiler compiler;

            public List<int> slots;

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

            public void SetTop(int top) {
                while (compiler.stack.Count < top) {
                    compiler.stack.Add(null);
                }
                compiler.maxStack = Math.Max(compiler.maxStack, compiler.stack.Count);
                while (compiler.stack.Count > top) {
                    compiler.stack.RemoveAt(compiler.stack.Count - 1);
                }
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

        void ParseVarSuffix(ParseState state, int src) {
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
                        var result = ParseExpression(state);
                        code.Add(Build3(SETTABLE, src, constIdx, result));
                        state.Pop(result);
                        state.Pop(src);
                    } else if (PeekComma()) {
                        ParseVar(state);
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
                        var result = ParseExpression(state);
                        code.Add(Build3(SETTABLE, src, indexer, result));
                        state.Pop(indexer);
                        state.Pop(src);
                    } else if (PeekComma()) {
                        ParseVar(state);
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    var func = src;
                    if (!state.IsTemporary(src)) {
                        func = state.Push();
                        code.Add(Build2(MOVE, func, src));
                    }
                    var nArgs = 0;
                    while (Peek() != TokenType.CloseParen) {
                        nArgs++;
                        var argSlot = state.Push();
                        var argResult = ParseExpression(state);
                        AdjustDestination(state, argResult, argSlot);
                        if (Peek() == TokenType.Comma) {
                            Next();
                        } else if (Peek() != TokenType.CloseParen) {
                            throw new Exception("Expected ',' or ')', but got " + Peek());
                        }
                    }
                    Next();
                    if (PeekSuffix()) {
                        code.Add(Build3(CALL, func, nArgs + 1, 2));
                        state.SetTop(func + 1);
                        ParseVarSuffix(state, src);
                    } else if (PeekAssignment() || PeekComma() || state.slots.Count > 1) {
                        throw new Exception("Cannot assign to function call");
                    } else {
                        code.Add(Build3(CALL, func, nArgs + 1, 1));
                        state.SetTop(func);
                    }
                    break;
                }
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        int ParseExpressionSuffix(ParseState state, int src) {
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
                    var func = src;
                    if (!state.IsTemporary(src)) {
                        func = state.Push();
                        code.Add(Build2(MOVE, func, src));
                    }
                    var nArgs = 0;
                    while (Peek() != TokenType.CloseParen) {
                        nArgs++;
                        var arg = ParseExpression(state);
                        state.SetTop(func + nArgs);
                        AdjustDestination(state, arg, func + nArgs);
                        if (Peek() == TokenType.Comma) {
                            Next();
                        } else if (Peek() != TokenType.CloseParen) {
                            throw new Exception("Expected ',' or ')' but got " + Peek());
                        }
                    }
                    Next();
                    int ret;
                    if (PeekSuffix()) {
                        code.Add(Build3(CALL, func, nArgs + 1, 2));
                        state.SetTop(func + 1);
                        ret = ParseExpressionSuffix(state, func);
                    } else {
                        code.Add(Build3(CALL, func, nArgs + 1, 2));
                        state.SetTop(func + 1);
                        ret = func;
                    }
                    return ret;
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
                case TokenType.Number: return Constant(double.Parse(token.text!));
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
                case TokenType.Nil: return Literal(LOADNIL, 1);
                default:
                    throw new Exception($"Unexpected token {token.type}");
            }
        }

        void ParseVar(ParseState state) {
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
                            var result = ParseExpression(state);
                            code.Add(Build3(SETTABUP, state.EnvUp(), constIdx, result));
                            state.Pop(result);
                        } else if (PeekComma()) {
                            ParseVar(state);
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
                            ParseVar(state);
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

    }
}

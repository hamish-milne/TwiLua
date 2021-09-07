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

    struct Token {
        public TokenType type;
        public string? text;
        public double number;

        public static implicit operator Token(TokenType type) => new Token { type = type };
    }

    class Lexer
    {
        public Lexer(string source) {
            str = source;
        }

        int position;
        string str;

        Stack<Token> tokens = new Stack<Token>();

        public Token Next() {
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

        public string ParseString(char term) {
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

        public TokenType Peek() {
            if (tokens.Count == 0) {
                tokens.Push(Next());
            }
            return tokens.Peek().type;
        }

        public string? Expect(TokenType type, string after) {
            var token = Next();
            if (token.type != type) {
                throw new Exception($"Expected {type} after {after}, but got {token.type}");
            }
            return token.text;
        }
    }

    public class Compiler
    {
        readonly Lexer lexer;
        readonly List<int> code = new List<int>(1024);
        readonly Compiler? parent;
        readonly List<LuaValue> constants = new List<LuaValue>();
        readonly List<string> locals = new List<string>();

        Compiler(string str) {
            lexer = new Lexer(str);
        }

        int Top;
        int nSlots;
        int nResults;
        int firstResult;
        int lastValue;
        int maxStack;

        int TopResult => Top + nResults;
        int Base => constants.Count;

        void SetTop(int top) => Top = top;

        int EnvUp() => 0;

        int Push() {
            throw new NotImplementedException();
        }

        void Pop(int idx) {
            throw new NotImplementedException();
        }

        bool IsTemporary(int idx) {
            throw new NotImplementedException();
        }

        Token Next() => lexer.Next();
        TokenType Peek() => lexer.Peek();
        string? Expect(TokenType type, string after) => lexer.Expect(type, after);
        string ParseString(char term) => lexer.ParseString(term);


        public static LuaFunction Compile(string str) {
            var c = new Compiler(str);
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
            switch (Peek()) {
                case TokenType.Identifier:
                case TokenType.OpenParen:
                case TokenType.OpenBrace:
                case TokenType.Minus:
                    ParseVar();
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

        void AdjustDestination(int old, int newIdx) {
            if (old == newIdx) {
                return;
            }
            if ((old & KFlag) != 0) {
                var constIdx = old & ~KFlag;
                code.Add(Build2x(LOADK, newIdx, constIdx));
            } else {
                code.Add(Build2(MOVE, newIdx, old));
                Pop(old);
                Pop(newIdx);
            }
        }

        int PreCall(int src) {
            var func = src;
            if (!IsTemporary(src)) {
                func = Push();
                code.Add(Build2(MOVE, func, src));
            }
            return func;
        }

        int ParseArgs(int func) {
            var nArgs = 0;
            while (Peek() != TokenType.CloseParen) {
                nArgs++;
                var arg = ParseExpression();
                SetTop(func + 1 + nArgs);
                AdjustDestination(arg, func + nArgs);
                if (Peek() == TokenType.Comma) {
                    Next();
                } else if (Peek() != TokenType.CloseParen) {
                    throw new Exception("Expected ',' or ')' but got " + Peek());
                }
            }
            Next();
            return nArgs;
        }

        void ExtendMultiReturn() {
            var inst = code[code.Count - 1];
            switch (Instruction.GetOpCode(inst)) {
                case CALL: {
                    var a = Instruction.GetA(inst);
                    var b = Instruction.GetB(inst);
                    code[code.Count - 1] = Build3(CALL, a, b, nSlots - nResults + 2);
                    nResults = nSlots;
                    break;
                }
                case VARARG: {
                    var a = Instruction.GetA(inst);
                    code[code.Count - 1] = Build2(VARARG, a, nSlots - nResults + 2);
                    nResults = nSlots;
                    break;
                }
            }
        }

        void ParseAssignment() {
            firstResult = Top;
            nResults = 0;
            while (true) {
                nResults++;
                lastValue = ParseExpression();
                if (Peek() == TokenType.Comma) {
                    Top = firstResult + nResults;
                    AdjustDestination(lastValue, firstResult + nResults - 1);
                    Next();
                } else {
                    break;
                }
            }
            if (nResults != nSlots) {
                SetTop(firstResult + nResults);
                AdjustDestination(lastValue, firstResult + nResults - 1);
                if (nResults < nSlots) {
                    ExtendMultiReturn();
                }
                if (nResults < nSlots) {
                    code.Add(Build2(LOADNIL, firstResult + nResults, firstResult + nSlots - nResults - 1));
                }
                lastValue = firstResult + nSlots - 1;
                SetTop(firstResult + nSlots);
                nResults = nSlots;
            }
        }

        void PopAssignment() {
            Debug.Assert(nResults > 0);
            if (nResults == nSlots && lastValue != TopResult) {
                nResults--;
                return; // Last value is not in the stack
            }
            Pop(firstResult + nResults - 1);
            nResults--;
        }
            

        void ParseVarSuffix(int src) {

            void PostCall(int func, int nArgs) {
                if (PeekSuffix()) {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 1);
                    ParseVarSuffix(src);
                } else if (PeekAssignment() || PeekComma() || nSlots > 1) {
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
                        if (IsTemporary(src)) {
                            code.Add(Build3(GETTABLE, src, src, constIdx));
                            ParseVarSuffix(src);
                        } else {
                            var dst = Push();
                            code.Add(Build3(GETTABLE, dst, src, constIdx));
                            ParseVarSuffix(dst);
                        }
                    } else if (PeekAssignment()) {
                        ParseAssignment();
                        code.Add(Build3(SETTABLE, src, constIdx, lastValue));
                        PopAssignment();
                        Pop(src);
                    } else if (PeekComma()) {
                        ParseVarAdditional();
                        code.Add(Build3(SETTABLE, src, constIdx, TopResult));
                        PopAssignment();
                        Pop(src);
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenBracket: {
                    var indexer = ParseExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    if (PeekSuffix()) {
                        if (IsTemporary(src)) {
                            code.Add(Build3(GETTABLE, src, src, indexer));
                            ParseVarSuffix(src);
                        } else {
                            var dst = Push();
                            code.Add(Build3(GETTABLE, dst, src, indexer));
                            ParseVarSuffix(dst);
                        }
                    } else if (PeekAssignment()) {
                        ParseAssignment();
                        code.Add(Build3(SETTABLE, src, indexer, lastValue));
                        PopAssignment();
                        Pop(indexer);
                        Pop(src);
                    } else if (PeekComma()) {
                        ParseVarAdditional();
                        code.Add(Build3(SETTABLE, src, indexer, TopResult));
                        PopAssignment();
                        Pop(indexer);
                        Pop(src);
                    } else {
                        throw new Exception($"Unexpected token {token.type}");
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    var func = PreCall(src);
                    var nArgs = ParseArgs(func);
                    PostCall(func, nArgs);
                    break;
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    var func = PreCall(src);
                    var arg = Constant(ParseString(token.type == TokenType.SingleQuote ? '\'' : '"'));
                    SetTop(func + 2);
                    AdjustDestination(arg, func + 1);
                    PostCall(func, 1);
                    break;
                }
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        int ParseExpressionSuffix(int src) {

            int PostCall(int func, int nArgs) {
                int ret;
                if (PeekSuffix()) {
                    code.Add(Build3(CALL, func, nArgs + 1, 2));
                    SetTop(func + 2);
                    ret = ParseExpressionSuffix(func);
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
                    var dst = IsTemporary(src) ? src : Push();
                    code.Add(Build3(GETTABLE, dst, src, indexer));
                    return ParseExpressionSuffix(dst);
                }
                case TokenType.OpenBracket: {
                    Next();
                    var indexer = ParseExpression();
                    Expect(TokenType.CloseBracket, "index expression");
                    var dst = IsTemporary(src) ? src : Push();
                    code.Add(Build3(GETTABLE, dst, src, indexer));
                    return ParseExpressionSuffix(dst);
                }
                case TokenType.OpenParen: {
                    Next();
                    var func = PreCall(src);
                    var nArgs = ParseArgs(func);
                    return PostCall(func, nArgs);
                }
                case TokenType.SingleQuote:
                case TokenType.DoubleQuote: {
                    Next();
                    var func = PreCall(src);
                    var arg = Constant(ParseString(Peek() == TokenType.SingleQuote ? '\'' : '"'));
                    SetTop(func + 2);
                    AdjustDestination(arg, func + 1);
                    return PostCall(func, 1);
                }
                default:
                    return src;
            }
        }

        int ParseExpression() {
            return ParseTerm();
        }

        int ParseTerm() {
            int Unary(OpCode op) {
                var expr = ParseTerm();
                var idx = Push();
                code.Add(Build2(op, idx, expr));
                return idx;
            }

            int Literal(OpCode op, int arg) {
                var idx = Push();
                code.Add(Build2(op, idx, arg));
                return idx;
            }

            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var localIdx = GetLocal(name);
                    if (localIdx != null) {
                        return ParseExpressionSuffix(localIdx.Value);
                    } else {
                        var globalIdx = Constant(name);
                        var idx = Push();
                        code.Add(Build3(GETTABUP, idx, EnvUp(), globalIdx));
                        return ParseExpressionSuffix(idx);
                    }
                }
                case TokenType.Number: return Constant(token.number);
                case TokenType.DoubleQuote: return Constant(ParseString('"'));
                case TokenType.SingleQuote: return Constant(ParseString('\''));
                case TokenType.OpenParen: {
                    var expr = ParseExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    return ParseExpressionSuffix(expr);
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

        void ParseVarAdditional() {
            ParseVar();
        }

        void ParseVar() {
            
            nSlots++;
            var token = Next();
            switch (token.type) {
                case TokenType.Identifier: {
                    var name = token.text!;
                    var index = GetLocal(name);
                    if (index == null) {
                        var constIdx = Constant(name);
                        if (PeekSuffix()) {
                            var dst = Push();
                            code.Add(Build3(GETTABUP, dst, EnvUp(), constIdx));
                            ParseVarSuffix(dst);
                        } else if (PeekAssignment()) {
                            ParseAssignment();
                            code.Add(Build3(SETTABUP, EnvUp(), constIdx, lastValue));
                            PopAssignment();
                        } else if (PeekComma()) {
                            ParseVarAdditional();
                            code.Add(Build3(SETTABUP, EnvUp(), constIdx, TopResult));
                            PopAssignment();
                        } else {
                            throw new Exception("Expected an call or assignment");
                        }
                    } else {
                        if (PeekSuffix()) {
                            ParseVarSuffix(index.Value);
                        } else if (PeekAssignment()) {
                            var result = ParseExpression();
                            AdjustDestination(result, index.Value);
                        } else if (PeekComma()) {
                            ParseVarAdditional();
                            AdjustDestination(TopResult, index.Value);
                        } else {
                            throw new Exception("Expected an call or assignment");
                        }
                    }
                    break;
                }
                case TokenType.OpenParen: {
                    var result = ParseExpression();
                    Expect(TokenType.CloseParen, "parenthesized expression");
                    if (PeekSuffix()) {
                        ParseVarSuffix(result);
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
                ParseAssignment();
                if (lastValue != TopResult) {
                    AdjustDestination(lastValue, Push());
                }
                Debug.Assert(firstResult == startR);
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

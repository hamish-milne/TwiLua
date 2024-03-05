using System;
using System.Collections.Generic;
using System.Text;
using static TwiLua.TokenType;

namespace TwiLua
{
    sealed class Lexer
    {
        public Lexer(string source) {
            str = source;
        }

        int position;
        int lines;
        int lineStart;
        readonly string str;

        readonly Stack<Token> tokens = new();

        public void PushBack(Token token) => tokens.Push(token);

        Token TwoCharToken(TokenType token, char c, TokenType token2) {
            if (position < str.Length && str[position] == c) {
                position++;
                return T(token2, 2);
            }
            return T(token);
        }

        Token TwoCharToken2(TokenType token, char c2, TokenType token2, char c3, TokenType token3) {
            if (position < str.Length && str[position] == c2) {
                position++;
                return T(token2, 2);
            }
            if (position < str.Length && str[position] == c3) {
                position++;
                return T(token3, 3);
            }
            return T(token);
        }

        Token T(TokenType type, int length = 1) {
            return new Token(type, new(lines, position - lineStart - length));
        }

        public Token Next() {
            if (tokens.Count > 0) {
                return tokens.Pop();
            }

            if (position >= str.Length) {
                return T(Eof);
            }

            char c = str[position++];

            if (position == 1 && c == '#') {
                while (position < str.Length && str[position] != '\n') {
                    position++;
                }
                lines++;
                lineStart = position + 1;
                position++;
                return Next();
            }

            if (position < str.Length && c == '-' && str[position] == '-') {
                bool isLongComment = false;
                position++;
                if (position < str.Length - 1 && str[position] == '[' && str[position + 1] == '[') {
                    position += 2;
                    isLongComment = true;
                }
                while (position < str.Length && str[position] != '\n') {
                    position++;
                }
                lines++;
                lineStart = position + 1;
                if (isLongComment) {
                    do {
                        position++;
                        if (position >= str.Length - 1) {
                            throw new Exception("unterminated long comment");
                        }
                        if (str[position] == '\n') {
                            lines++;
                            lineStart = position + 1;
                        }
                    } while (str[position] != ']' || str[position + 1] != ']');
                    position += 2;
                } else {
                    position++;
                }
                return Next();
            }

            if (char.IsWhiteSpace(c)) {
                position--;
                do {
                    if (str[position] == '\n') {
                        lines++;
                        lineStart = position + 1;
                    }
                    position++;
                } while (position < str.Length && char.IsWhiteSpace(str[position]));
                return Next();
            }

            var start = position - 1;
            if (char.IsLetter(c) || c == '_') {
                while (position < str.Length && (char.IsLetter(str[position]) || char.IsDigit(str[position]) || str[position] == '_')) {
                    position++;
                }
                var identifier = str.Substring(start, position - start);
                var simpleToken = identifier switch {
                    "and" => And,
                    "or" => Or,
                    "not" => Not,
                    "if" => If,
                    "then" => Then,
                    "else" => Else,
                    "elseif" => ElseIf,
                    "for" => For,
                    "do" => Do,
                    "while" => While,
                    "end" => End,
                    "return" => Return,
                    "local" => Local,
                    "function" => Function,
                    "true" => True,
                    "false" => False,
                    "nil" => Nil,
                    "break" => Break,
                    "repeat" => Repeat,
                    "until" => Until,
                    "in" => In,
                    _ => Identifier
                };
                return simpleToken switch
                {
                    Identifier => new Token(identifier, new(lines, start - lineStart)),
                    _ => T(simpleToken),
                };

            }

            if (c == '0' && position < str.Length - 1 && (str[position] == 'x' || str[position] == 'X')) {
                position++;
                do {
                    if (position >= str.Length) {
                        break;
                    }
                    c = str[position++];
                } while (char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
                start += 2;
                return new Token(
                    long.Parse(str.Substring(start, position - start), System.Globalization.NumberStyles.HexNumber),
                    new(lines, start - lineStart)
                );
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
                return new Token(
                    mantissa * Math.Pow(10, exponent),
                    new(lines, start - lineStart)
                );
            }

            switch (c) {
                case '\'': return T(SingleQuote);
                case '"': return T(DoubleQuote);
                case '(': return T(OpenParen);
                case ')': return T(CloseParen);
                case '[': return T(OpenBracket);
                case ']': return T(CloseBracket);
                case '{': return T(OpenBrace);
                case '}': return T(CloseBrace);
                case ',': return T(Comma);
                case '.':
                    if (position < str.Length && str[position] == '.') {
                        position++;
                        if (position < str.Length && str[position] == '.') {
                            position++;
                            return T(TripleDot);
                        }
                        return T(DoubleDot);
                    }
                    return T(Dot);
                case ':': return T(Colon);
                case ';': return T(Semicolon);
                case '+': return T(Plus);
                case '-': return T(Minus);
                case '*': return T(Star);
                case '/': return TwoCharToken(Slash, '/', DoubleSlash);
                case '%': return T(Percent);
                case '^': return T(Caret);
                case '~': return TwoCharToken(Tilde, '=', NotEqual);
                case '=': return TwoCharToken(Equal, '=', DoubleEqual);
                case '<': return TwoCharToken2(LessThan, '=', LessThanEqual, '<', ShiftLeft);
                case '>': return TwoCharToken2(GreaterThan, '=', GreaterThanEqual, '>', ShiftRight);
                case '#': return T(Hash);
                case '&': return T(Ampersand);
                case '|': return T(Pipe);
                default:
                    throw new Exception($"Unexpected character '{c}' at position {position}");
            }
        }

        public (string value, Location location) ParseString(char term) {
            var sb = new StringBuilder();
            var start = position;
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
                        case '\n':
                            lines++;
                            lineStart = position;
                            break;
                        default:
                            throw new Exception($"Invalid escape sequence '\\{c}'");
                    }
                } else if (c == term) {
                    break;
                } else {
                    sb.Append(c);
                }
            }
            return (sb.ToString(), new Location(lines, start - lineStart));
        }

        public Token Peek() {
            if (tokens.Count == 0) {
                tokens.Push(Next());
            }
            return tokens.Peek();
        }

        public string? Expect(TokenType type, string after) {
            var token = Next();
            if (token.type != type) {
                throw new Exception($"Expected {type} after {after}, but got {token.type}");
            }
            return token.text;
        }
    }
}

using System;
using System.Collections.Generic;

namespace TwiLua
{

    struct PatternData
    {
        string pattern;
        string text;
        int i;
        int j;
        Stack<(int i, int j, int nCapture, int nCapturePending)>? backtrack;
        Stack<int>? capturesPending;
        List<(int, int)>? captures;

        bool MatchClassSingle(char c, bool allowRanges)
        {
            switch (pattern[i++])
            {
                case '.':
                    return true;
                case '%':
                    CheckLength();
                    return pattern[i++] switch
                    {
                        'a' => char.IsLetter(c),
                        'c' => char.IsControl(c),
                        'd' => char.IsDigit(c),
                        'l' => char.IsLower(c),
                        'p' => char.IsPunctuation(c),
                        's' => char.IsWhiteSpace(c),
                        'u' => char.IsUpper(c),
                        'w' => char.IsLetterOrDigit(c),
                        'x' => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'),
                        _ => char.IsLetterOrDigit(c) ? throw Malformed() : c == pattern[i - 1],
                    };
                default:
                    if (allowRanges && i+1 < pattern.Length && pattern[i] == '-') {
                        i++;
                        var start = pattern[i-2];
                        var end = pattern[i++];
                        if (start > end) {
                            (end, start) = (start, end);
                        }
                        return c >= start && c <= end;
                    }
                    return c == pattern[i-1];
            }
        }

        readonly Exception Malformed() {
            return new Exception($"malformed pattern (unexpected '{pattern[i-1]}' at position {i})");
        }

        readonly void CheckLength() {
            if (i >= pattern.Length) {
                throw Malformed();
            }
        }

        bool MatchClass(char c)
        {
            if (pattern[i] != '[') {
                return MatchClassSingle(c, false);
            }
            i++;
            CheckLength();
            var invert = pattern[i] == '^';
            if (invert) {
                i++;
                CheckLength();
            }
            var matched = false;
            while (i < pattern.Length) {
                if (pattern[i] == ']') {
                    i++;
                    return matched ^ invert;
                }
                matched |= MatchClassSingle(c, true);
            }
            throw Malformed();
        }

        bool Match()
        {
            while (i < pattern.Length) {
                if (!MatchItem()) {
                    if (backtrack?.Count > 0) {
                        var (oldI, oldJ, oldNCapture, oldNCapturePending) = backtrack.Pop();
                        i = oldI;
                        j = oldJ;
                        captures?.RemoveRange(oldNCapture, captures.Count - oldNCapture);
                        while (capturesPending?.Count > oldNCapturePending) {
                            capturesPending.Pop();
                        }
                    } else {
                        return false;
                    }
                }
            }
            return true;
        }

        void PushBacktrack(int oldI, int oldJ) {
            backtrack ??= new();
            backtrack.Push((oldI, oldJ, captures?.Count ?? 0, capturesPending?.Count ?? 0));
        }

        bool MatchCapture(int n)
        {
            if (captures == null || n < 1 || n > captures.Count) {
                throw Malformed();
            }
            var (start, end) = captures[n-1];
            if (end == -1) {
                return false;
            }
            var oldI = i;
            var oldJ = j;
            i = start;
            j = end;
            var result = Match();
            i = oldI;
            j = oldJ;
            return result;
        }

        bool MatchItem()
        {
            if (i == 0) {
                if (pattern[i] == '^') {
                    i++;
                    return true;
                }
                PushBacktrack(0, j + 1);
            }
            switch (pattern[i])
            {
                case '$':
                    if (j >= text.Length) {
                        return true;
                    }
                    goto default;
                case '(':
                    i++;
                    if (pattern[i] == ')') {
                        i++;
                        captures ??= new();
                        captures.Add((-(j + 1), 0));
                        return true;
                    }
                    return true;
                case ')':
                    i++;
                    capturesPending ??= new();
                    if (capturesPending.Count == 0) {
                        throw Malformed();
                    }
                    if (capturesPending.Count > 0) {
                        var n = capturesPending.Pop();
                        captures![n] = (captures[n].Item1, j);
                    }
                    return true;
                case '%':
                    i++;
                    CheckLength();
                    if (char.IsDigit(pattern[i])) {
                        return MatchCapture(pattern[i] - '0');
                    }
                    if (pattern[i] == 'f') {
                        i++;
                        // Frontier pattern
                        var c0 = j == 0 ? '\0' : text[j-1];
                        var c1 = j >= text.Length ? '\0' : text[j];
                        return !MatchClass(c0) && MatchClass(c1);
                    }
                    i--;
                    goto default;
                default: {
                    var itemStart = i;
                    var matched = MatchClass(text[j]);
                    if (i >= pattern.Length) {
                        return matched;
                    }
                    switch (pattern[i]) {
                        case '?':
                            if (matched) {
                                j++;
                            }
                            i++;
                            return true;
                        case '*':
                            if (matched) {
                                do {
                                    j++;
                                    i = itemStart;
                                } while (j < text.Length && MatchClass(text[j]));
                            }
                            i++;
                            return true;
                        case '+':
                            if (!matched) {
                                i++;
                                return false;
                            }
                            do {
                                i = itemStart;
                            } while (j < text.Length && MatchClass(text[j]));
                            i++;
                            return true;
                        case '-':
                            if (matched) {
                                PushBacktrack(itemStart, j + 1);
                            }
                            i++;
                            return true;
                        default:
                            if (matched) {
                                j++;
                            }
                            return matched;
                    }
                }
            }
        }
    }

    public class PatternException : LuaException
    {
        public PatternException(string message) : base(message) { }
    }
}
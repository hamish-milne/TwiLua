namespace YANCL
{

    public struct Pattern
    {
        string pattern;
        string text;
        int i;
        int j;

        bool MatchClass(bool allowRanges)
        {
            if (j >= text.Length || i >= pattern.Length) {
                return false;
            }
            var c = text[j];
            switch (pattern[i++])
            {
                case '.':
                    return true;
                case '%':
                    if (i >= pattern.Length) {
                        return c == '%';
                    }
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
                        _ => c == pattern[i - 1],
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

        bool MatchSet()
        {
            var matched = false;
            while (i < pattern.Length) {
                switch (pattern[i])
                {
                    case ']':
                        i++;
                        return matched;
                    default:
                        matched |= MatchClass(true);
                        break;
                }
            }
            throw new System.Exception("unterminated set");
        }

        bool MatchItem()
        {
            if (i >= pattern.Length) {
                return false;
            }
            switch (pattern[i])
            {
                case '[':
                    i++;
                    if (i < pattern.Length && pattern[i] == '^') {
                        i++;
                        return !MatchSet();
                    }
                    return MatchSet();
                case '%':
                    i++;
                    return MatchClass(false);
                default:
                    return MatchClass(false);
            }
        }
    }
}
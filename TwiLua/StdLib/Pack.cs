using System;
using System.Text;

namespace TwiLua
{
    enum PackType
    {
        Padding,
        Signed,
        Unsigned,
        Float,
        StringFixed,
        StringNullTerminated,
        StringLengthPrefixed,
    }

    struct PackData
    {
        static readonly Encoding binaryEncoding = Encoding.GetEncoding(28591);
        static readonly Encoding textEncoding = Encoding.UTF8;
        readonly string format;
        readonly int nativeAlignment;
        readonly int nativeSize;
        readonly LuaThread? thread;
        readonly int argOffset;
        int i, j;
        bool littleEndian;
        int alignment;

        int MatchDigit(int? defaultValue) {
            return i < format.Length && format[i] > '0' && format[i] < '9'
                ? format[i++] - '0'
                : (defaultValue ?? throw new Exception($"Expected digit at position {i} of format string '{format}'"));
        }

        ref LuaValue GetArg() {
            if (thread == null) {
                throw new Exception("No thread");
            }
            if (j >= thread.Count) {
                throw new ArgumentOutOfRangeException();
            }
            return ref thread[j++];
        }

        (PackType type, int? size)? MatchOne()
        {
            if (i >= format.Length) {
                throw new Exception($"Malformed format '{format}', ends with ${format[i-1]}");
            }
            switch (format[i++]) {
                case ' ':
                    return null;
                case '<':
                    littleEndian = true;
                    return null;
                case '>':
                    littleEndian = false;
                    return null;
                case '=':
                    littleEndian = BitConverter.IsLittleEndian;
                    return null;
                case '!':
                    alignment = MatchDigit(nativeAlignment);
                    return null;
                case 'b':
                    return (PackType.Signed, 1);
                case 'B':
                    return (PackType.Unsigned, 1);
                case 'h':
                    return (PackType.Signed, 2);
                case 'H':
                    return (PackType.Unsigned, 2);
                case 'l':
                    return (PackType.Signed, 4);
                case 'L':
                    return (PackType.Unsigned, 4);
                case 'j':
                    return (PackType.Signed, 8);
                case 'J':
                case 'T':
                    return (PackType.Unsigned, 8);
                case 'i':
                    return (PackType.Signed, MatchDigit(nativeSize));
                case 'I':
                    return (PackType.Unsigned, MatchDigit(nativeSize));
                case 'f':
                    return (PackType.Float, 4);
                case 'd':
                case 'n':
                    return (PackType.Float, 8);
                case 'c':
                    return (PackType.StringFixed, MatchDigit(null));
                case 'z':
                    return (PackType.StringNullTerminated, null);
                case 's':
                    return (PackType.StringLengthPrefixed, MatchDigit(null));
                case 'x':
                    return (PackType.Padding, 1);
                case 'X':
                    return (PackType.Padding, MatchOne()?.size ?? throw new Exception($"Expected size at position {i} of format string '{format}'"));
                default:
                    throw new Exception($"Unknown format character '{format[i-1]}' at position {i}");
            }
        }

        int Size()
        {
            j = argOffset;
            i = 0;
            var size = 0;
            while (i < format.Length) {
                var next = MatchOne();
                if (next == null) {
                    continue;
                }
                var nextSize = next.Value.size;
                if (thread != null) {
                    nextSize = next.Value.type switch
                    {
                        PackType.StringNullTerminated => textEncoding.GetByteCount(GetArg().ExpectString()) + 1,
                        PackType.StringLengthPrefixed => textEncoding.GetByteCount(GetArg().ExpectString()) + nextSize,
                        _ => nextSize,
                    };
                }
                size += nextSize ?? throw new Exception($"Only fixed-size types are allowed in a size calculation, found {format[i-1]}");
            }
            return size;
        }

        static void WriteInt(ulong value, int n, byte[] bytes, ref int k) {
            for (var i = 0; i < n; i++) {
                bytes[k++] = (byte)(value >> (8 * i));
            }
        }

        static ulong ReadInt(byte[] bytes, ref int k, int n) {
            ulong value = 0;
            for (var i = 0; i < n; i++) {
                value |= (ulong)bytes[k++] << (8 * i);
            }
            return value;
        }

        string Pack()
        {
            if (thread == null) {
                throw new Exception("No thread");
            }
            var bytes = new byte[Size()];
            j = argOffset;
            i = 0;
            var k = 0;
            while (i < format.Length) {
                var next = MatchOne();
                if (next == null) {
                    continue;
                }
                var (type, size) = next.Value;
                switch (type) {
                    case PackType.StringNullTerminated: {
                        var str = GetArg().ExpectString();
                        if (str.Contains("\0")) {
                            throw new Exception("String contains null byte");
                        }
                        textEncoding.GetBytes(str, 0, str.Length, bytes, k);
                        k += str.Length;
                        bytes[k++] = 0;
                        break;
                    }
                }
                var nextSize = size ?? throw new Exception();
                switch (type) {
                    case PackType.Padding:
                        k += nextSize;
                        break;
                    case PackType.Signed:
                    case PackType.Unsigned:
                        WriteInt((ulong)GetArg().ExpectInteger(), nextSize, bytes, ref k);
                        break;
                    case PackType.Float:
                        switch (nextSize) {
                            case 8:
                                WriteInt((ulong)BitConverter.DoubleToInt64Bits(GetArg().ExpectNumber()), 8, bytes, ref k);
                                break;
                            case 4:
                                BitConverter.GetBytes((float)GetArg().ExpectNumber()).CopyTo(bytes, k);
                                k += 4;
                                break;
                            default:
                                throw new Exception();
                        }
                        break;
                    case PackType.StringFixed: {
                        var str = GetArg().ExpectString();
                        var count = textEncoding.GetByteCount(str);
                        if (count != nextSize) {
                            throw new Exception($"Expected string of length {nextSize}, got {count}");
                        }
                        textEncoding.GetBytes(str, 0, str.Length, bytes, k);
                        k += str.Length;
                        break;
                    }
                    case PackType.StringLengthPrefixed: {
                        var str = GetArg().ExpectString();
                        var count = textEncoding.GetByteCount(str);
                        WriteInt((ulong)count, nextSize, bytes, ref k);
                        textEncoding.GetBytes(str, 0, str.Length, bytes, k);
                        k += str.Length;
                        break;
                    }
                    default:
                        throw new Exception();
                }
            }
            return binaryEncoding.GetString(bytes);
        }

        void Unpack(string input)
        {
            if (thread == null) {
                throw new Exception("No thread");
            }
            var bytes = binaryEncoding.GetBytes(input);
            j = argOffset;
            i = 0;
            var k = 0;
            while (i < format.Length) {
                var next = MatchOne();
                if (next == null) {
                    continue;
                }
                var (type, size) = next.Value;
                switch (type) {
                    case PackType.StringNullTerminated: {
                        var len = 0;
                        while (bytes[k + len] != 0) {
                            len++;
                        }
                        GetArg() = textEncoding.GetString(bytes, k, len);
                        break;
                    }
                }
                var nextSize = size ?? throw new Exception();
                switch (type) {
                    case PackType.Padding:
                        k += nextSize;
                        break;
                    case PackType.Unsigned:
                        GetArg() = ReadInt(bytes, ref k, nextSize);
                        k += nextSize;
                        break;
                    case PackType.Signed: {
                        var remaining = 64 - nextSize * 8;
                        GetArg() = (long)ReadInt(bytes, ref k, nextSize) << remaining >> remaining;
                        k += nextSize;
                        break;
                    }
                    case PackType.Float:
                        switch (nextSize) {
                            case 8:
                                GetArg() = BitConverter.Int64BitsToDouble(BitConverter.ToInt64(bytes, k));
                                k += 8;
                                break;
                            case 4:
                                GetArg() = BitConverter.ToSingle(bytes, k);
                                k += 4;
                                break;
                            default:
                                throw new Exception();
                        }
                        break;
                    case PackType.StringFixed: {
                        GetArg() = textEncoding.GetString(bytes, k, nextSize);
                        k += nextSize;
                        break;
                    }
                    case PackType.StringLengthPrefixed: {
                        var len = (int)ReadInt(bytes, ref k, nextSize);
                        k += nextSize;
                        GetArg() = textEncoding.GetString(bytes, k, len);
                        k += len;
                        break;
                    }
                    default:
                        throw new Exception();
                }
            }
        }
    }
}
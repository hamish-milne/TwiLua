using System.Runtime.CompilerServices;

namespace YANCL
{
    public static class Instruction {
        public const int KFlag = 0x100;
        const int BOffset = 0b100000000000000000 - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OpCode GetOpCode(int instruction) {
            return (OpCode)(instruction & 0b111111);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetA(int instruction) {
            return (instruction >> 6) & 0b11111111;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetC(int instruction) {
            return (instruction >> 6+8) & 0b111111111; 
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetB(int instruction) {
            return (instruction >> 6+8+9) & 0b111111111;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBx(int instruction) {
            return (instruction >> 6+8) & 0b111111111_111111111;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSbx(int instruction) {
            return ((instruction >> 6+8) & 0b111111111_111111111) - BOffset;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAx(int instruction) {
            return instruction >> 6;
        }

        public static int Build1(OpCode opCode, int a) {
            return (int)opCode | (a << 6);
        }

        public static int Build2(OpCode opCode, int a, int b) {
            return (int)opCode | (a << 6) | (b << 6+8+9);
        }

        public static int Build2x(OpCode opCode, int a, int b) {
            return (int)opCode | (a << 6) | (b << 6+8);
        }

        public static int Build2sx(OpCode opCode, int a, int b) {
            return (int)opCode | (a << 6) | ((b + BOffset) << 6+8);
        }

        public static int Build3(OpCode opCode, int a, int b, int c) {
            return (int)opCode | (a << 6) | (c << 6+8) | (b << 6+8+9);
        }

        public static string Stringify(int instruction) {
            var opCode = GetOpCode(instruction);
            var a = GetA(instruction);
            var b = GetB(instruction);
            var c = GetC(instruction);
            var bx = GetBx(instruction);
            var sbx = GetSbx(instruction);
            var ax = GetAx(instruction);
            if ((b & KFlag) != 0) {
                b = -1 - (b & ~KFlag);
            }
            if ((c & KFlag) != 0) {
                c = -1 - (c & ~KFlag);
            }
            return $"{opCode}\n    A: {a}\n    B: {b}\n    C: {c}\n    Bx: {bx}\n    Sbx: {sbx}\n    Ax: {ax}";
        }
    }
}

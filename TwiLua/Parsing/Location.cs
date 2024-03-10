using System;

namespace TwiLua
{
    public readonly struct Location : IEquatable<Location>
    {
        public int Line { get; }
        public int Column { get; }
        public Location(int line, int column) {
            Line = line;
            Column = column;
        }

        public readonly override string ToString() => $"{Line+1}:{Column+1}";

        public readonly bool Equals(Location other) => Line == other.Line && Column == other.Column;
        public readonly override bool Equals(object? obj) => obj is Location other && Equals(other);
        public readonly override int GetHashCode() => HashCode.Combine(Line, Column);
        public static bool operator ==(Location left, Location right) => left.Equals(right);
        public static bool operator !=(Location left, Location right) => !left.Equals(right);
    }
}
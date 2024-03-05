namespace TwiLua
{
    public readonly struct Location
    {
        public int Line { get; }
        public int Column { get; }
        public Location(int line, int column) {
            Line = line;
            Column = column;
        }

        public readonly override string ToString() => $"{Line+1}:{Column+1}";
    }
}
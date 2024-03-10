using System;

namespace TwiLua
{
    public sealed class LuaRuntimeError : Exception {
        public LuaValue Value { get; }

        public LuaRuntimeError(in LuaValue value) {
            Value = value;
        }

        public override string Message => $"Runtime error: {Value}";
    }
}
using System;
using System.IO;
using System.Text;

namespace TwiLua
{
    public static class FunctionUtils
    {
        static readonly byte[] LuaSignature = new byte[]{ 0x1B, (byte)'L', (byte)'u', (byte)'a' };
        const byte LuaVersion = 0x54;
        const byte FormatVersion = 0;
        static readonly byte[] MagicBytes = new byte[] { 0x19, 0x93, 0x0D, 0x0A, 0x1A, 0x0A };
        const long TestInteger = 0x5678;
        const double TestFloat = 370.5;

        static void DumpHeader(BinaryWriter writer)
        {
            writer.Write(LuaSignature);
            writer.Write(LuaVersion);
            writer.Write(FormatVersion);
            writer.Write(MagicBytes);
            writer.Write((byte)sizeof(int));
            writer.Write((byte)sizeof(long));
            writer.Write((byte)sizeof(double));
            writer.Write(TestInteger);
            writer.Write(TestFloat);
        }

        static bool CheckBytes(BinaryReader reader, byte[] expected)
        {
            foreach (byte v in expected) {
                if (reader.ReadByte() != v) {
                    return false;
                }
            }
            return true;
        }

        static void ReadHeader(BinaryReader reader)
        {
            if (!CheckBytes(reader, LuaSignature)) {
                throw new NotSupportedException("not a binary chunk");
            }
            if (reader.ReadByte() != LuaVersion) {
                throw new NotSupportedException("version mismatch");
            }
            if (reader.ReadByte() != FormatVersion) {
                throw new NotSupportedException("format mismatch");
            }
            if (!CheckBytes(reader, MagicBytes)) {
                throw new NotSupportedException("corrupted chunk");
            }
            if (reader.ReadByte() != sizeof(int)) {
                throw new NotSupportedException("Instruction size mismatch");
            }
            if (reader.ReadByte() != sizeof(long)) {
                throw new NotSupportedException("Integer size mismatch");
            }
            if (reader.ReadByte() != sizeof(double)) {
                throw new NotSupportedException("Number size mismatch");
            }
            if (reader.ReadInt64() != TestInteger) {
                throw new NotSupportedException("integer format mismatch");
            }
            if (reader.ReadDouble() != TestFloat) {
                throw new NotSupportedException("float format mismatch");
            }
        }

        static void DumpSize(BinaryWriter writer, int size)
        {
            while (size >= 0x80)
            {
                writer.Write((byte)size);
                size >>= 7;
            }
            writer.Write((byte)(size | 0x80));
        }

        static int ReadSize(BinaryReader reader)
        {
            int size = 0;
            int shift = 0;
            while (true)
            {
                byte b = reader.ReadByte();
                size |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return size;
                }
                shift += 7;
            }
        }

        static void DumpString(BinaryWriter writer, string str)
        {
            DumpSize(writer, str.Length);
            writer.Write(str);
        }

        static string ReadString(BinaryReader reader)
        {
            int size = ReadSize(reader);
            return new string(reader.ReadChars(size));
        }

        static void DumpConstant(BinaryWriter writer, LuaValue luaValue)
        {
            switch (luaValue.Type)
            {
                case TypeTag.Nil:
                    writer.Write((byte)0);
                    break;
                case TypeTag.False:
                    writer.Write((byte)1);
                    break;
                case TypeTag.True:
                    writer.Write((byte)17);
                    break;
                case TypeTag.Number:
                    if (luaValue.TryGetInteger(out var i))
                    {
                        writer.Write((byte)3);
                        writer.Write(i);
                    }
                    else
                    {
                        writer.Write((byte)19);
                        writer.Write(luaValue.Number);
                    }
                    break;
                default:
                    switch (luaValue.Object)
                    {
                        case string str:
                            writer.Write((byte)4);
                            DumpString(writer, str);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported constant: {luaValue}");
                    }
                    break;
            }
        }

        static LuaValue ReadConstant(BinaryReader reader)
        {
            byte tag = reader.ReadByte();
            return tag switch
            {
                0 => LuaValue.Nil,
                1 => false,
                17 => true,
                3 => reader.ReadInt64(),
                19 => reader.ReadDouble(),
                4 => ReadString(reader),
                _ => throw new NotSupportedException($"Unsupported constant tag: {tag}"),
            };
        }

        static void DumpFunction(BinaryWriter writer, LuaFunction func, string? parentSource)
        {
            DumpString(writer, func.chunkName == parentSource ? "" : func.chunkName);
            var locations = func.locations;
            writer.Write(locations.Length == 0 ? 0 : locations[0].Line);
            writer.Write(locations.Length == 0 ? 0 : locations[locations.Length - 1].Line);
            writer.Write((byte)func.nParams);
            writer.Write(func.IsVaradic);
            writer.Write((byte)func.nSlots);
            var code = func.code;
            writer.Write(code.Length);
            foreach (int v in code) {
                writer.Write(v);
            }
            writer.Write(func.constants.Length);
            foreach (var constant in func.constants) {
                DumpConstant(writer, constant);
            }
            writer.Write(func.upvalues.Length);
            foreach (var upvalue in func.upvalues) {
                writer.Write(upvalue.InStack);
                writer.Write((byte)upvalue.Index);
                writer.Write((byte)0); // Upvalue 'kind'
            }
            writer.Write(func.prototypes.Length);
            foreach (var prototype in func.prototypes) {
                DumpFunction(writer, prototype, func.chunkName);
            }
            writer.Write(0); // lineinfo
            writer.Write(0); // abslineinfo
            writer.Write(func.locals.Length);
            foreach (var local in func.locals) {
                DumpString(writer, local.Name);
                writer.Write(local.Start);
                writer.Write(local.End);
            }
            writer.Write(func.upvalues.Length);
            foreach (var upvalue in func.upvalues) {
                DumpString(writer, upvalue.Name);
            }
        }

        static LuaFunction ReadFunction(BinaryReader reader, string? parentSource)
        {
            var chunkName = ReadString(reader);
            var firstLine = reader.ReadInt32();
            var lastLine = reader.ReadInt32();
            var nParams = reader.ReadByte();
            var isVaradic = reader.ReadBoolean();
            var nSlots = reader.ReadByte();
            var code = new int[reader.ReadInt32()];
            for (int i = 0; i < code.Length; i++) {
                code[i] = reader.ReadInt32();
            }
            var constants = new LuaValue[reader.ReadInt32()];
            for (int i = 0; i < constants.Length; i++) {
                constants[i] = ReadConstant(reader);
            }
            var upvalues = new UpValueInfo[reader.ReadInt32()];
            for (int i = 0; i < upvalues.Length; i++) {
                upvalues[i] = new UpValueInfo("", reader.ReadBoolean(), reader.ReadByte());
                reader.ReadByte(); // Upvalue 'kind'
            }
            var prototypes = new LuaFunction[reader.ReadInt32()];
            for (int i = 0; i < prototypes.Length; i++) {
                prototypes[i] = ReadFunction(reader, chunkName);
            }
            reader.ReadInt32(); // lineinfo
            reader.ReadInt32(); // abslineinfo
            var locals = new LocalVarInfo[reader.ReadInt32()];
            for (int i = 0; i < locals.Length; i++) {
                locals[i] = new LocalVarInfo(ReadString(reader), reader.ReadInt32(), reader.ReadInt32());
            }
            var upvalueNameCount = reader.ReadInt32();
            for (int i = 0; i < upvalueNameCount; i++) {
                upvalues[i] = new UpValueInfo(ReadString(reader), upvalues[i].InStack, upvalues[i].Index);
            }
            return new LuaFunction {
                chunkName = chunkName,
                locations = Array.Empty<Location>(),
                nParams = nParams,
                IsVaradic = isVaradic,
                nSlots = nSlots,
                code = code,
                constants = constants,
                upvalues = upvalues,
                prototypes = prototypes,
                locals = locals,
            };
        }

        public static void Dump(this LuaFunction func, Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            DumpHeader(writer);
            DumpFunction(writer, func, null);
        }

        public static LuaFunction Load(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            ReadHeader(reader);
            return ReadFunction(reader, null);
        }
    }
}
using System;
using static System.Math;

namespace YANCL.StdLib {

    public static class Math {

        public static void Load(LuaTable globals) {
            var random = new Random();
            var rndBuf = new byte[sizeof(double)];

            globals["math"] = new LuaTable {
                {"abs", s => s.Return(Abs(s.Number()))},
                {"acos", s => s.Return(Acos(s.Number()))},
                {"asin", s => s.Return(Asin(s.Number()))},
                {"atan", s => s.Return(Atan(s.Number()))},
                {"ceil", s => s.Return(Ceiling(s.Number()))},
                {"cos", s => s.Return(Cos(s.Number()))},
                {"floor", s => s.Return(Floor(s.Number()))},
                {"deg", s => s.Return(s.Number() * 180 / PI)},
                {"rad", s => s.Return(s.Number() * PI / 180)},
                {"exp", s => s.Return(Exp(s.Number()))},
                {"sin", s => s.Return(Sin(s.Number()))},
                {"tan", s => s.Return(Tan(s.Number()))},
                {"sqrt", s => s.Return(Sqrt(s.Number()))},
                {"pi", PI},
                {"huge", double.PositiveInfinity},
                {"maxinteger", Pow(2, 53)},
                {"mininteger", -Pow(2, 53)},
                {"fmod", s => s.Return(s.Number(0) % s.Number(1))},
                {"ult", s => s.Return((ulong)s.Integer(1) < (ulong)s.Integer(2))},
                {"random", s => {
                    switch (s.Count) {
                        case 0:
                            s.Return(random.NextDouble());
                            break;
                        case 1:
                            var n = s.Integer(1);
                            if (n == 0) {
                                random.NextBytes(rndBuf);
                                s.Return(BitConverter.ToDouble(rndBuf, 0));
                            } else {
                                s.Return(random.Next(1, (int)n));
                            }
                            break;
                        case 2:
                            s.Return(random.Next((int)s.Integer(1), (int)s.Integer(2)));
                            break;
                        default:
                            throw new WrongNumberOfArguments();
                    }
                }},
                {"randomseed", s => {
                    switch (s.Count) {
                        case 0:
                            random = new Random();
                            break;
                        case 1:
                        case 2:
                            random = new Random((int)(s.Integer(1) ^ s.Integer(2)));
                            break;
                        default:
                            throw new WrongNumberOfArguments();
                    }
                    s.Count = 0;
                }},
                {"tointeger", s => {
                    try {
                        s.Return(s.Integer());
                    } catch (NoIntegerRepresentation) {
                        s.Return(LuaValue.Nil);
                    }
                }},
                {"type", s => {
                    if (s[1].Type == LuaType.NUMBER) {
                        if (s[1].Number % 1 == 0.0) {
                            s.Return("integer");
                        } else {
                            s.Return("float");
                        }
                    } else {
                        s.Return(LuaValue.Nil);
                    }
                }},
                {"max", s => {
                    var a = s.Number(1);
                    for (int i = 2; i <= s.Count; i++) {
                        a = Max(a, s.Number(i));
                    }
                    s.Return(a);
                }},
                {"min", s => {
                    var a = s.Number(0);
                    for (int i = 2; i <= s.Count; i++) {
                        a = Min(a, s.Number(i));
                    }
                    s.Return(a);
                }}
            };
        }
    }
}
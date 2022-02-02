using System;

namespace YANCL
{
    static class Program
    {
        static void Main(string[] args)
        {
            var lua = new Lua();
            StdLib.Basic.Load(lua.Globals);
            StdLib.Math.Load(lua.Globals);
            StdLib.String.Load(lua.Globals);
            StdLib.Table.Load(lua.Globals);
            while (true) {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (line == null)
                    return;
                try {
                    var result = lua.DoString(line);
                    if (result != null)
                        foreach (var v in result)
                            Console.WriteLine(v);
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
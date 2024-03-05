using System;

namespace TwiLua
{
    static class Program
    {
        static void Main(string[] args)
        {
            var lua = new Lua().LoadLibs();
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
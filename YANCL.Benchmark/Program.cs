using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace YANCL.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
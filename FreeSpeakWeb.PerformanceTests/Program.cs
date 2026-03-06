using BenchmarkDotNet.Running;

namespace FreeSpeakWeb.PerformanceTests;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

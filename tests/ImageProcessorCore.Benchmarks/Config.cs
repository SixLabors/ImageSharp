using BenchmarkDotNet.Configs;

namespace ImageProcessorCore.Benchmarks
{
    public class Config : ManualConfig
    {
        public Config()
        {
            // Uncomment if you want to use any of the diagnoser
            this.Add(new BenchmarkDotNet.Diagnostics.Windows.MemoryDiagnoser());
            this.Add(new BenchmarkDotNet.Diagnostics.Windows.InliningDiagnoser());
        }
    }
}
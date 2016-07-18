using BenchmarkDotNet.Configs;

namespace ImageProcessorCore.Benchmarks
{
    public class Config : ManualConfig
    {
        public Config()
        {
            // uncomment if you want to use any of the diagnoser
            //Add(new BenchmarkDotNet.Diagnostics.Windows.MemoryDiagnoser());
            //Add(new BenchmarkDotNet.Diagnostics.Windows.InliningDiagnoser());
        }
    }
}
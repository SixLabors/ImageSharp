using BenchmarkDotNet.Attributes;

namespace ImageProcessorCore.Benchmarks
{
    using System.Drawing;

    using CoreColor = ImageProcessorCore.Color;

    public class Colors
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Color")]
        public bool SystemDrawingColorEqual()
        {
            return Color.FromArgb(128, 128, 128, 128).Equals(Color.FromArgb(128, 128, 128, 128));
        }

        [Benchmark(Description = "ImageProcessorCore Color")]
        public bool ColorEqual()
        {
            return new CoreColor(.5f, .5f, .5f, .5f).Equals(new CoreColor(.5f, .5f, .5f, .5f));
        }
    }
}

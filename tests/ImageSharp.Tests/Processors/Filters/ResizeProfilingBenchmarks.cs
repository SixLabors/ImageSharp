namespace ImageSharp.Tests
{
    using System.IO;
    using System.Text;

    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using ImageSharp.Processing.Processors;

    using Xunit;
    using Xunit.Abstractions;

    public class ResizeProfilingBenchmarks : MeasureFixture
    {
        public ResizeProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        public int ExecutionCount { get; set; } = 50;

        // [Theory] // Benchmark, enable manually!
        [InlineData(100, 100)]
        [InlineData(2000, 2000)]
        public void ResizeBicubic(int width, int height)
        {
            this.Measure(this.ExecutionCount,
                () =>
                    {
                        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
                        {
                            image.Resize(width / 4, height / 4);
                        }
                    });
        }

        // [Fact]
        public void PrintWeightsData()
        {
            ResizeProcessor<Rgba32> proc = new ResizeProcessor<Rgba32>(new BicubicResampler(), 200, 200);

            ResamplingWeightedProcessor<Rgba32>.WeightsBuffer weights = proc.PrecomputeWeights(200, 500);

            StringBuilder bld = new StringBuilder();

            foreach (ResamplingWeightedProcessor<Rgba32>.WeightsWindow window in weights.Weights)
            {
                for (int i = 0; i < window.Length; i++)
                {
                    float value = window.Span[i];
                    bld.Append(value);
                    bld.Append("| ");
                }
                bld.AppendLine();
            }

            File.WriteAllText("BicubicWeights.MD", bld.ToString());

            //this.Output.WriteLine(bld.ToString());
        }
    }
}
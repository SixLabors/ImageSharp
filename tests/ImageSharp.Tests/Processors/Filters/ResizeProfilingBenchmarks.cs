namespace ImageSharp.Tests
{
    using System.IO;
    using System.Text;

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
                        using (Image image = new Image(width, height))
                        {
                            image.Resize(width / 4, height / 4);
                        }
                    });
        }

        // [Fact]
        public void PrintWeightsData()
        {
            ResizeProcessor<Color> proc = new ResizeProcessor<Color>(new BicubicResampler(), 200, 200);

            ResamplingWeightedProcessor<Color>.WeightsBuffer weights = proc.PrecomputeWeights(200, 500);

            StringBuilder bld = new StringBuilder();

            foreach (ResamplingWeightedProcessor<Color>.WeightsWindow window in weights.Weights)
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
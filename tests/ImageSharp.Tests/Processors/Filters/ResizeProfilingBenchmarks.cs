namespace ImageSharp.Tests
{
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
    }
}
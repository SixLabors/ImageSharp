namespace ImageProcessorCore.Benchmarks.Image
{
    using System.Threading.Tasks;

    using BenchmarkDotNet.Attributes;

    using CoreColor = ImageProcessorCore.Color;
    using CoreImage = ImageProcessorCore.Image;

    public class CopyPixels
    {
        [Benchmark(Description = "Copy by Pixel")]
        public CoreColor CopyByPixel()
        {
            CoreImage source = new CoreImage(1024, 768);
            CoreImage target = new CoreImage(1024, 768);
            using (PixelAccessor<CoreColor, uint> sourcePixels = source.Lock())
            using (PixelAccessor<CoreColor, uint> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    source.Height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < source.Width; x++)
                        {
                            targetPixels[x, y] = sourcePixels[x, y];
                        }
                    });

                return targetPixels[0, 0];
            }
        }

        [Benchmark(Description = "Copy by Row")]
        public CoreColor CopyByRow()
        {
            CoreImage source = new CoreImage(1024, 768);
            CoreImage target = new CoreImage(1024, 768);
            using (PixelAccessor<CoreColor, uint> sourcePixels = source.Lock())
            using (PixelAccessor<CoreColor, uint> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    source.Height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                    {
                        sourcePixels.CopyBlock(0, y, targetPixels, 0, y, source.Width);
                    });

                return targetPixels[0, 0];
            }
        }
    }
}

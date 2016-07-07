namespace ImageProcessorCore.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using BenchmarkDotNet.Attributes;
    using CoreSize = ImageProcessorCore.Size;

    public class Resize
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Resize")]
        public Size ResizeSystemDrawing()
        {
            using (Bitmap source = new Bitmap(400, 400))
            {
                using (Bitmap destination = new Bitmap(100, 100))
                {
                    using (Graphics graphics = Graphics.FromImage(destination))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.DrawImage(source, 0, 0, 100, 100);
                    }

                    return destination.Size;
                }
            }
        }

        [Benchmark(Description = "ImageProcessorCore Resize")]
        public CoreSize ResizeCore()
        {
            Image<Bgra32> image = new Image<Bgra32>(400, 400);
            image.Resize(100, 100);
            return new CoreSize(image.Width, image.Height);
        }
    }
}

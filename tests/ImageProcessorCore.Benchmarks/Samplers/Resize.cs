namespace ImageProcessorCore.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using BenchmarkDotNet.Attributes;
    using CoreImage = ImageProcessorCore.Image;
    using CoreSize = ImageProcessorCore.Size;

    public class Resize
    {
        [Benchmark(Baseline = true, Description = "System.Drawing Resize")]
        public Size ResizeSystemDrawing()
        {
            using (Bitmap source = new Bitmap(2000, 2000))
            {
                using (Bitmap destination = new Bitmap(400, 400))
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
            CoreImage image = new CoreImage(2000, 2000);
            image.Resize(400, 400);
            return new CoreSize(image.Width, image.Height);
        }
    }
}

namespace ImageProcessorCore.Benchmarks.Image
{
    using System.Drawing;

    using BenchmarkDotNet.Attributes;

    //using CoreColor = ImageProcessorCore.Color;
    //using CoreImage = ImageProcessorCore.Image<Bgra32>;
    using SystemColor = System.Drawing.Color;

    public class GetSetPixel
    {
        [Benchmark(Baseline = true, Description = "System.Drawing GetSet Pixel")]
        public SystemColor ResizeSystemDrawing()
        {
            using (Bitmap source = new Bitmap(400, 400))
            {
                source.SetPixel(200, 200, SystemColor.White);
                return source.GetPixel(200, 200);
            }
        }

        [Benchmark(Description = "ImageProcessorCore GetSet Pixel")]
        public Bgra32 ResizeCore()
        {
            Image<Bgra32, uint> image = new Image<Bgra32, uint>(400, 400);
            using (IPixelAccessor<Bgra32, uint> imagePixels = image.Lock())
            {
                imagePixels[200, 200] = new Bgra32(1, 1, 1, 1);
                return imagePixels[200, 200];
            }
        }
    }
}

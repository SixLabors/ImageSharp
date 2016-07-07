//namespace ImageProcessorCore.Benchmarks
//{
//    using System.Drawing;
//    using System.Drawing.Drawing2D;

//    using BenchmarkDotNet.Attributes;
//    using CoreImage = ImageProcessorCore.Image;
//    using CoreSize = ImageProcessorCore.Size;

//    public class Crop
//    {
//        [Benchmark(Baseline = true, Description = "System.Drawing Crop")]
//        public Size CropSystemDrawing()
//        {
//            using (Bitmap source = new Bitmap(400, 400))
//            {
//                using (Bitmap destination = new Bitmap(100, 100))
//                {
//                    using (Graphics graphics = Graphics.FromImage(destination))
//                    {
//                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
//                        graphics.CompositingQuality = CompositingQuality.HighQuality;
//                        graphics.DrawImage(source, new Rectangle(0, 0, 100, 100), 0, 0, 100, 100, GraphicsUnit.Pixel);
//                    }

//                    return destination.Size;
//                }
//            }
//        }

//        [Benchmark(Description = "ImageProcessorCore Crop")]
//        public CoreSize CropResizeCore()
//        {
//            CoreImage image = new CoreImage(400, 400);
//            image.Crop(100, 100);
//            return new CoreSize(image.Width, image.Height);
//        }
//    }
//}


namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.IO;
    using System.Numerics;

    using ImageProcessor.Filters;

    using Xunit;

    public class FilterTests : ProcessorTestBase
    {
        public static readonly TheoryData<string, IImageProcessor> Filters = new TheoryData<string, IImageProcessor>
        {
            //{ "Brightness-50", new Brightness(50) },
            //{ "Brightness--50", new Brightness(-50) },
            //{ "Contrast-50", new Contrast(50) },
            //{ "Contrast--50", new Contrast(-50) },
            //{ "Blend", new Blend(new Image(File.OpenRead("../../TestImages/Formats/Bmp/Car.bmp")),15)},
            //{ "Saturation-50", new Saturation(50) },
            //{ "Saturation--50", new Saturation(-50) },
            //{ "Alpha--50", new Alpha(50) },
            //{ "Invert", new Invert() },
            //{ "Sepia", new Sepia() },
            //{ "BlackWhite", new BlackWhite() },
            //{ "Lomograph", new Lomograph() },
            //{ "Polaroid", new Polaroid() },
            //{ "Kodachrome", new Kodachrome() },
            //{ "GreyscaleBt709", new GreyscaleBt709() },
            //{ "GreyscaleBt601", new GreyscaleBt601() },
            //{ "Kayyali", new Kayyali() },
            //{ "Kirsch", new Kirsch() },
            //{ "Laplacian3X3", new Laplacian3X3() },
            //{ "Laplacian5X5", new Laplacian5X5() },
            //{ "LaplacianOfGaussian", new LaplacianOfGaussian() },
            //{ "Prewitt", new Prewitt() },
            //{ "RobertsCross", new RobertsCross() },
            //{ "Scharr", new Scharr() },
            //{ "Sobel", new Sobel() },
            { "GuassianBlur", new GuassianBlur(10) },
            { "GuassianSharpen", new GuassianSharpen(10) }
        };

        [Theory]
        [MemberData("Filters")]
        public void FilterImage(string name, IImageProcessor processor)
        {
            if (!Directory.Exists("Filtered"))
            {
                Directory.CreateDirectory("Filtered");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"Filtered/{ Path.GetFileName(filename) }"))
                    {
                        image.Process(processor).Save(output);
                    }

                    Trace.WriteLine($"{ name }: { watch.ElapsedMilliseconds}ms");
                }
            }
        }
    }
}

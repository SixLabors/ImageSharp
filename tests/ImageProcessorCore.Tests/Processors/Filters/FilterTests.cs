
namespace ImageProcessorCore.Tests
{
    using System.Diagnostics;
    using System.IO;

    using ImageProcessorCore.Filters;

    using Xunit;

    public class FilterTests : FileTestBase
    {
        public static readonly TheoryData<string, IImageProcessor> Filters = new TheoryData<string, IImageProcessor>
        {
            { "Brightness-50", new Brightness(50) },
            { "Brightness--50", new Brightness(-50) },
            { "Contrast-50", new Contrast(50) },
            { "Contrast--50", new Contrast(-50) },
            { "BackgroundColor", new BackgroundColor(new Color(243 / 255f, 87 / 255f, 161 / 255f,.5f))},
            { "Blend", new Blend(new Image(File.OpenRead("TestImages/Formats/Bmp/Car.bmp")),50)},
            { "Saturation-50", new Saturation(50) },
            { "Saturation--50", new Saturation(-50) },
            { "Alpha--50", new Alpha(50) },
            { "Invert", new Invert() },
            { "Sepia", new Sepia() },
            { "BlackWhite", new BlackWhite() },
            { "Lomograph", new Lomograph() },
            { "Polaroid", new Polaroid() },
            { "Kodachrome", new Kodachrome() },
            { "GreyscaleBt709", new GreyscaleBt709() },
            { "GreyscaleBt601", new GreyscaleBt601() },
            { "Kayyali", new Kayyali() },
            { "Kirsch", new Kirsch() },
            { "Laplacian3X3", new Laplacian3X3() },
            { "Laplacian5X5", new Laplacian5X5() },
            { "LaplacianOfGaussian", new LaplacianOfGaussian() },
            { "Prewitt", new Prewitt() },
            { "RobertsCross", new RobertsCross() },
            { "Scharr", new Scharr() },
            { "Sobel", new Sobel {Greyscale = true} },
            { "Pixelate", new Pixelate(8)  },
            { "GuassianBlur", new GuassianBlur(10) },
            { "GuassianSharpen", new GuassianSharpen(10) },
            { "Hue-180", new Hue(180) },
            { "Hue--180", new Hue(-180) },
            { "BoxBlur", new BoxBlur(10) },
            { "Vignette", new Vignette() },
            { "Protanopia", new Protanopia() },
            { "Protanomaly", new Protanomaly() },
            { "Deuteranopia", new Deuteranopia() },
            { "Deuteranomaly", new Deuteranomaly() },
            { "Tritanopia", new Tritanopia() },
            { "Tritanomaly", new Tritanomaly() },
            { "Achromatopsia", new Achromatopsia() },
            { "Achromatomaly", new Achromatomaly() }

        };

        [Theory]
        [MemberData("Filters")]
        public void FilterImage(string name, IImageProcessor processor)
        {
            if (!Directory.Exists("TestOutput/Filter"))
            {
                Directory.CreateDirectory("TestOutput/Filter");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Filter/{ Path.GetFileName(filename) }"))
                    {
                        processor.OnProgress += this.ProgressUpdate;
                        image.Process(processor).Save(output);
                        processor.OnProgress -= this.ProgressUpdate;
                    }

                    Trace.WriteLine($"{ name }: { watch.ElapsedMilliseconds}ms");
                }
            }
        }
    }
}

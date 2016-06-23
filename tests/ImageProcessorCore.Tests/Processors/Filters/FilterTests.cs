
namespace ImageProcessorCore.Tests
{
    using System.Diagnostics;
    using System.IO;

    using Processors;

    using Xunit;

    public class FilterTests : FileTestBase
    {
        public static readonly TheoryData<string, IImageProcessor> Filters = new TheoryData<string, IImageProcessor>
        {
            { "Brightness-50", new BrightnessProcessor(50) },
            { "Brightness--50", new BrightnessProcessor(-50) },
            { "Contrast-50", new ContrastProcessor(50) },
            { "Contrast--50", new ContrastProcessor(-50) },
            { "BackgroundColor", new BackgroundColorProcessor(new Color(243 / 255f, 87 / 255f, 161 / 255f,.5f))},
            { "Blend", new BlendProcessor(new Image(File.OpenRead("TestImages/Formats/Bmp/Car.bmp")),50)},
            { "Saturation-50", new SaturationProcessor(50) },
            { "Saturation--50", new SaturationProcessor(-50) },
            { "Alpha--50", new AlphaProcessor(50) },
            { "Invert", new InvertProcessor() },
            { "Sepia", new SepiaProcessor() },
            { "BlackWhite", new BlackWhiteProcessor() },
            { "Lomograph", new LomographProcessor() },
            { "Polaroid", new PolaroidProcessor() },
            { "Kodachrome", new KodachromeProcessor() },
            { "GreyscaleBt709", new GreyscaleBt709Processor() },
            { "GreyscaleBt601", new GreyscaleBt601Processor() },
            { "Kayyali", new KayyaliProcessor() },
            { "Kirsch", new KirschProcessor() },
            { "Laplacian3X3", new Laplacian3X3Processor() },
            { "Laplacian5X5", new Laplacian5X5Processor() },
            { "LaplacianOfGaussian", new LaplacianOfGaussianProcessor() },
            { "Prewitt", new PrewittProcessor() },
            { "RobertsCross", new RobertsCrossProcessor() },
            { "Scharr", new ScharrProcessor() },
            { "Sobel", new SobelProcessor {Greyscale = true} },
            { "Pixelate", new PixelateProcessor(8)  },
            { "GuassianBlur", new GuassianBlurProcessor(10) },
            { "GuassianSharpen", new GuassianSharpenProcessor(10) },
            { "Hue-180", new HueProcessor(180) },
            { "Hue--180", new HueProcessor(-180) },
            { "BoxBlur", new BoxBlurProcessor(10) },
            { "Vignette", new VignetteProcessor() },
            { "Protanopia", new ProtanopiaProcessor() },
            { "Protanomaly", new ProtanomalyProcessor() },
            { "Deuteranopia", new DeuteranopiaProcessor() },
            { "Deuteranomaly", new DeuteranomalyProcessor() },
            { "Tritanopia", new TritanopiaProcessor() },
            { "Tritanomaly", new TritanomalyProcessor() },
            { "Achromatopsia", new AchromatopsiaProcessor() },
            { "Achromatomaly", new AchromatomalyProcessor() }

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
                    using (FileStream output = File.OpenWrite($"TestOutput/Filter/{Path.GetFileName(filename)}"))
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

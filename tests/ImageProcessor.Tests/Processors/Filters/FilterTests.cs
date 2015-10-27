
namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.IO;

    using ImageProcessor.Filters;

    using Xunit;

    public class FilterTests : ProcessorTestBase
    {
        public static readonly TheoryData<string, IImageProcessor> Filters = new TheoryData<string, IImageProcessor>
        {
            { "Contrast-50", new Contrast(50) },
            { "Contrast--50", new Contrast(-50) },
            { "Alpha--50", new Alpha(50) },
            { "Invert", new Invert() },
            { "Sepia", new Sepia() },
            { "BlackWhite", new BlackWhite() },
            { "Lomograph", new Lomograph() },
            { "Polaroid", new Polaroid() },
            { "GreyscaleBt709", new GreyscaleBt709() },
            { "GreyscaleBt601", new GreyscaleBt601() },
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

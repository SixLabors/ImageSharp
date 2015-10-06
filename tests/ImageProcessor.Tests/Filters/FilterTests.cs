
namespace ImageProcessor.Tests.Filters
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    using ImageProcessor.Filters;
    using ImageProcessor.Samplers;

    using Xunit;

    public class FilterTests
    {
        public static readonly List<string> Files = new List<string>
        {
            //{ "../../TestImages/Formats/Jpg/Backdrop.jpg"},
            //{ "../../TestImages/Formats/Bmp/Car.bmp" },
            //{ "../../TestImages/Formats/Png/cmyk.png" },
            //{ "../../TestImages/Formats/Gif/a.gif" },
            //{ "../../TestImages/Formats/Gif/leaf.gif" },
            { "../../TestImages/Formats/Gif/ani.gif" },
            //{ "../../TestImages/Formats/Gif/ani2.gif" },
            //{ "../../TestImages/Formats/Gif/giphy.gif" },
        };

        public static readonly TheoryData<string, IImageProcessor> Filters = new TheoryData<string, IImageProcessor>
        {
            { "Contrast-50", new Contrast(50) },
            { "Contrast--50", new Contrast(-50) },
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

        [Fact]
        public void ResizeImage()
        {
            if (!Directory.Exists("Resized"))
            {
                Directory.CreateDirectory("Resized");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileName(file);
                    using (FileStream output = File.OpenWrite($"Resized/{ Path.GetFileName(filename) }"))
                    {
                        image.Resize(400, 400).Save(output);
                    }

                    Trace.WriteLine($"{ filename }: { watch.ElapsedMilliseconds}ms");
                }
            }
        }
    }
}

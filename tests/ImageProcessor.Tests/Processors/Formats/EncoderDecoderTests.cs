namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.IO;

    using Formats;

    using Xunit;

    public class EncoderDecoderTests : ProcessorTestBase
    {
        [Fact]
        public void DecodeThenEncodeImageFromStreamShouldSucceed()
        {
            if (!Directory.Exists("Encoded"))
            {
                Directory.CreateDirectory("Encoded");
            }

            foreach (FileInfo file in new DirectoryInfo("Encoded").GetFiles())
            {
                file.Delete();
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);

                    string encodedFilename = "Encoded/" + Path.GetFileName(file);

                    using (FileStream output = File.OpenWrite(encodedFilename))
                    {
                        image.Save(output);
                    }

                    Trace.WriteLine($"{file} : {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void QuantizedImageShouldPreserveMaximumColorPrecision()
        {
            if (!Directory.Exists("Quantized"))
            {
                Directory.CreateDirectory("Quantized");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    IQuantizer quantizer = new OctreeQuantizer();
                    QuantizedImage quantizedImage = quantizer.Quantize(image);

                    using (FileStream output = File.OpenWrite($"Quantized/{Path.GetFileName(file)}"))
                    {
                        quantizedImage.ToImage().Save(output);
                    }
                }
            }
        }
    }
}
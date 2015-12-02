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
            if (!Directory.Exists("TestOutput/Encode"))
            {
                Directory.CreateDirectory("TestOutput/Encode");
            }

            foreach (FileInfo file in new DirectoryInfo("TestOutput/Encode").GetFiles())
            {
                file.Delete();
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);

                    string encodeFilename = "TestOutput/Encode/" + Path.GetFileName(file);

                    using (FileStream output = File.OpenWrite(encodeFilename))
                    {
                        image.Save(output);
                    }

                    Trace.WriteLine($"{file} : {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void QuantizeImageShouldPreserveMaximumColorPrecision()
        {
            if (!Directory.Exists("TestOutput/Quantize"))
            {
                Directory.CreateDirectory("TestOutput/Quantize");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    IQuantizer quantizer = new OctreeQuantizer();
                    QuantizedImage quantizedImage = quantizer.Quantize(image);

                    using (FileStream output = File.OpenWrite($"TestOutput/Quantize/{Path.GetFileName(file)}"))
                    {
                        quantizedImage.ToImage().Save(output);
                    }
                }
            }
        }
    }
}
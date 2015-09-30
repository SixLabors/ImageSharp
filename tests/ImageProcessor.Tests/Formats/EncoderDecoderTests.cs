namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using ImageProcessor.Formats;

    using Xunit;

    public class EncoderDecoderTests
    {
        [Theory]
        //[InlineData("TestImages/Car.bmp")]
        //[InlineData("TestImages/Portrait.png")]
        [InlineData("../../TestImages/Formats/Jpg/Backdrop.jpg")]
        [InlineData("../../TestImages/Formats/Gif/leaf.gif")]
        //[InlineData("TestImages/Windmill.gif")]
        //[InlineData("../../TestImages/Formats/Bmp/Car.bmp")]
        //[InlineData("../../TestImages/Formats/Png/cmyk.png")]
        public void DecodeThenEncodeImageFromStreamShouldSucceed(string filename)
        {
            if (!Directory.Exists("Encoded"))
            {
                Directory.CreateDirectory("Encoded");
            }

            FileStream stream = File.OpenRead(filename);
            Stopwatch watch = Stopwatch.StartNew();
            Image image = new Image(stream);

            OctreeQuantizer quantizer = new OctreeQuantizer();
            var o = quantizer.Quantize(image);

            using (MemoryStream s2 = new MemoryStream())
            {
                LzwEncoder2 enc2 = new LzwEncoder2(image.Width, image.Height, o.Pixels, 8);
                enc2.Encode(s2);
                using (MemoryStream s = new MemoryStream())
                {
                    LzwEncoder enc = new LzwEncoder(o.Pixels, 8);
                    enc.Encode(s);

                    var x = s.ToArray();
                    var y = s2.ToArray();

                    var a = x.Skip(1080);
                    var b = y.Skip(1080);

                    Assert.Equal(s.ToArray(), s2.ToArray());
                }
            }

            string encodedFilename = "Encoded/" + Path.GetFileNameWithoutExtension(filename) + ".jpg";

            //if (!image.IsAnimated)
            //{
            using (FileStream output = File.OpenWrite(encodedFilename))
            {
                IImageEncoder encoder = Image.Encoders.First(e => e.IsSupportedFileExtension(".jpg"));
                encoder.Encode(image, output);
            }
            //}
            //else
            //{
            //    using (var output = File.OpenWrite(
            //        string.Format("Encoded/{ Path.GetFileNameWithoutExtension(filename) }.jpg"))
            //    {
            //        image.SaveAsJpeg(output, 40);
            //    }

            //    for (int i = 0; i < image.Frames.Count; i++)
            //    {
            //        using (var output = File.OpenWrite($"Encoded/{ i }_{ Path.GetFileNameWithoutExtension(filename) }.png"))
            //        {
            //            image.Frames[i].SaveAsPng(output);
            //        }
            //    }
            //}

            Trace.WriteLine($"{filename} : {watch.ElapsedMilliseconds}ms");
        }

        [Theory]
        [InlineData("../../TestImages/Formats/Jpg/Backdrop.jpg")]
        [InlineData("../../TestImages/Formats/Bmp/Car.bmp")]
        [InlineData("../../TestImages/Formats/Png/cmyk.png")]
        public void QuantizedImageShouldPreserveMaximumColorPrecision(string filename)
        {
            if (!Directory.Exists("Quantized"))
            {
                Directory.CreateDirectory("Quantized");
            }

            Image image = new Image(File.OpenRead(filename));
            IQuantizer quantizer = new OctreeQuantizer();
            QuantizedImage quantizedImage = quantizer.Quantize(image);

            using (FileStream output = File.OpenWrite($"Quantized/{ Path.GetFileName(filename) }"))
            {
                IImageEncoder encoder = Image.Encoders.First(e => e.IsSupportedFileExtension(Path.GetExtension(filename)));
                encoder.Encode(quantizedImage.ToImage(), output);
            }
        }
    }
}
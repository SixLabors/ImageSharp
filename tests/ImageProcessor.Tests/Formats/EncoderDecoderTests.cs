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
        //[InlineData("TestImages/Backdrop.jpg")]
        //[InlineData("TestImages/Windmill.gif")]
        [InlineData("../../TestImages/Formats/Png/cmyk.png")]
        public void DecodeThenEncodeImageFromStreamShouldSucceed(string filename)
        {
            if (!Directory.Exists("Encoded"))
            {
                Directory.CreateDirectory("Encoded");
            }

            FileStream stream = File.OpenRead(filename);
            Stopwatch watch = Stopwatch.StartNew();
            Image image = new Image(stream);

            string encodedFilename = "Encoded/" + Path.GetFileName(filename);

            //if (!image.IsAnimated)
            //{
            using (FileStream output = File.OpenWrite(encodedFilename))
            {
                IImageEncoder encoder = Image.Encoders.First(e => e.IsSupportedFileExtension(Path.GetExtension(filename)));
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

            Trace.WriteLine(string.Format("{0} : {1}ms", filename, watch.ElapsedMilliseconds));
        }
    }
}
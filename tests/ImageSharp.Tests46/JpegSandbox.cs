using System;
using System.IO;
using ImageSharp.Formats;
using Xunit;

namespace ImageSharp.Tests
{
    public class JpegSandbox
    {
        public const string SandboxOutputDirectory = "_SandboxOutput";

        protected string CreateTestOutputFile(string fileName)
        {
            if (!Directory.Exists(SandboxOutputDirectory))
            {
                Directory.CreateDirectory(SandboxOutputDirectory);
            }

            string id = Guid.NewGuid().ToString().Substring(0, 4);

            string ext = Path.GetExtension(fileName);
            fileName = Path.GetFileNameWithoutExtension(fileName);

            return $"{SandboxOutputDirectory}/{fileName}_{id}{ext}";
        }

        protected Stream CreateOutputStream(string fileName)
        {
            fileName = CreateTestOutputFile(fileName);
            return File.OpenWrite(fileName);
        }

        [Fact]
        public void OpenJpeg_SaveBmp()
        {
            var image = new TestFile(TestImages.Jpeg.Calliphora).CreateImage();

            using (var stream = CreateOutputStream(nameof(OpenJpeg_SaveBmp)+".bmp"))
            {
                image.Save(stream, new BmpFormat());
            }
        }
    }
}
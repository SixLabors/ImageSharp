using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests
{
    public class JpegSandbox
    {
        
        public const string SandboxOutputDirectory = "_SandboxOutput";

        private ITestOutputHelper Output { get; }

        public JpegSandbox(ITestOutputHelper output)
        {
            Output = output;
        }

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
            Output?.WriteLine("Opened for write: "+fileName);
            return File.OpenWrite(fileName);
        }

        //public static string[][] AllJpegFiles = new[]
        //{
        //    TestImages.Jpeg.All
        //};

        public static IEnumerable<object[]> AllJpegFiles => TestImages.Jpeg.All.Select(fn => new object[] {fn});

        [Theory]
        [MemberData(nameof(AllJpegFiles))]
        public void OpenJpeg_SaveBmp(string jpegFileName)
        {
            var image = new TestFile(jpegFileName).CreateImage();
            string bmpFileName = Path.GetFileNameWithoutExtension(jpegFileName) + ".bmp";

            using (var stream = CreateOutputStream(bmpFileName))
            {
                image.Save(stream, new BmpFormat());
            }
        }

        [Fact]
        public void Boo()
        {
            Vector<int> hej = new Vector<int>();
            
            

            Output.WriteLine(Vector<int>.Count.ToString());
        }
        
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    public class JpegTests
    {
        
        public const string TestOutputDirectory = "TestOutput/Jpeg";

        private ITestOutputHelper Output { get; }

        public JpegTests(ITestOutputHelper output)
        {
            Output = output;
        }

        protected string CreateTestOutputFile(string fileName)
        {
            if (!Directory.Exists(TestOutputDirectory))
            {
                Directory.CreateDirectory(TestOutputDirectory);
            }

            //string id = Guid.NewGuid().ToString().Substring(0, 4);

            string ext = Path.GetExtension(fileName);
            fileName = Path.GetFileNameWithoutExtension(fileName);

            return $"{TestOutputDirectory}/{fileName}{ext}";
        }

        protected Stream CreateOutputStream(string fileName)
        {
            fileName = CreateTestOutputFile(fileName);
            Output?.WriteLine("Opened for write: "+fileName);
            return File.OpenWrite(fileName);
        }

        public static IEnumerable<object[]> AllJpegFiles
            => TestImages.Jpeg.All.Select(file => new object[] {TestFile.Create(file)});

        [Theory]
        [MemberData(nameof(AllJpegFiles))]
        public void OpenJpeg_SaveBmp(TestFile file)
        {
            string bmpFileName = file.FileNameWithoutExtension + ".bmp";

            var image = file.CreateImage();
                
            using (var outputStream = CreateOutputStream(bmpFileName))
            {
                image.Save(outputStream, new BmpFormat());
            }
        }

        public static IEnumerable<object[]> AllBmpFiles
            => TestImages.Bmp.All.Select(file => new object[] { TestFile.Create(file) });

        [Theory]
        [MemberData(nameof(AllBmpFiles))]
        public void OpenBmp_SaveJpeg(TestFile file)
        {
            string jpegPath = file.FileNameWithoutExtension + ".jpeg";

            var image = file.CreateImage();

            using (var outputStream = CreateOutputStream(jpegPath))
            {
                image.Save(outputStream, new JpegFormat());
            }
        }
    }
}
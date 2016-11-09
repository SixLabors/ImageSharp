using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using ImageSharp.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests46.Benchmark
{
    using System.Drawing;
    using System.IO;

    using CoreImage = ImageSharp.Image;
    using CoreSize = ImageSharp.Size;

    public class DecodeJpeg
    {
        private static byte[] jpegBytes = File.ReadAllBytes(TestImages.Jpeg.Calliphora);

        private ITestOutputHelper _output;

        public DecodeJpeg(ITestOutputHelper output)
        {
            _output = output;
        }

        private void DoBenchmark(int times, Action<Stream> action, [CallerMemberName]string method = null)
        {
            _output.WriteLine($"Starting {method}.. ");
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < times; i++)
            {
                using (MemoryStream memoryStream = new MemoryStream(jpegBytes))
                {
                    action(memoryStream);
                }
            }
            sw.Stop();
            var millis = sw.ElapsedMilliseconds;

            _output.WriteLine($"{method} finished in {millis}ms");
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        public void JpegSystemDrawing(int times)
        {
            DoBenchmark(times, memoryStream =>
            {
                Image image = Image.FromStream(memoryStream);
                image.Dispose();
            });
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        public void JpegCore(int times)
        {
            DoBenchmark(times, memoryStream =>
            {
                CoreImage image = new CoreImage(memoryStream);
            });
        }

    }
}
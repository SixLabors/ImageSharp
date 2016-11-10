using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageSharp.Tests;
using ImageSharp.Tests46.Benchmark;
using Xunit.Abstractions;

namespace ConsoleBenchmark
{
    class Program
    {
        private class Output : ITestOutputHelper
        {
            public void WriteLine(string message)
            {
                Console.WriteLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                Console.WriteLine(format, args);
            }
        }

        static void Main(string[] args)
        {
            DecodeJpegBenchmark benchmark = new DecodeJpegBenchmark(new Output());
            benchmark.JpegCore(100);
            //JpegSandbox test = new JpegSandbox(null);

            //test.OpenJpeg_SaveBmp(TestImages.Jpeg.Calliphora);
        }
    }
}

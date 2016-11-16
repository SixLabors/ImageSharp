using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using ImageSharp;

namespace JpegBenchmark
{
    public class Program
    {
        private const string TestImageDir = @"../ImageSharp.Tests/TestImages/Formats/Jpg";

        private static byte[][] ReadAllFiles()
        {
            var files = Directory.EnumerateFiles(TestImageDir).ToArray();
            return files.Select(File.ReadAllBytes).ToArray();
        }

        public static void Main(string[] args)
        {
            var allData = ReadAllFiles();
            int times;
            if (args.Length == 0 || !int.TryParse(args[0], out times))
            {
                times = 20;
            }

            Console.WriteLine($"Vector.IsHardwareAccelerated == {Vector.IsHardwareAccelerated}");

            Console.WriteLine($"Decoding {allData.Length} jpegs X {times} times ...");

            double estimatedTotalBlockCount = 0;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < times; i++)
            {
                estimatedTotalBlockCount = DecodeAll(allData);
            }
            
            sw.Stop();

            Console.WriteLine($"Completed in {sw.ElapsedMilliseconds} ms");

            Console.WriteLine($"Estimated block count: {estimatedTotalBlockCount}");
            Console.ReadLine();
        }

        private static double DecodeAll(byte[][] allData)
        {
            double estimatedTotalBlockCount = 0;
            foreach (byte[] data in allData)
            {
                var stream = new MemoryStream(data);
                Image img = new Image(stream);

                estimatedTotalBlockCount += img.Width*img.Height/64.0;
            }
            return estimatedTotalBlockCount;
        }
    }
}

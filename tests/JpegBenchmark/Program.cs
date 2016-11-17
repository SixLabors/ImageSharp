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

            bool methodSystemDrawing = args.Length > 0 && args[0].ToLower().Contains("system");

            int times;
            if (args.Length < 2 || !int.TryParse(args[1], out times))
            {
                times = 20;
            }

            Console.WriteLine($"Vector.IsHardwareAccelerated == {Vector.IsHardwareAccelerated}");
            Console.WriteLine($"Method: {(methodSystemDrawing?"System.Drawing":"ImageSharp")}");

            Console.WriteLine($"Decoding {allData.Length} jpegs X {times} times ...");

            double estimatedTotalBlockCount = 0;

            Stopwatch sw = Stopwatch.StartNew();

            Func<byte[], double> method = methodSystemDrawing ? (Func<byte[], double>) DecodeMonoSystemDrawing : DecodeImageSharp;

            estimatedTotalBlockCount = DecodeAll(allData, times, method);


            sw.Stop();

            Console.WriteLine($"Completed in {sw.ElapsedMilliseconds} ms");

            Console.WriteLine($"Estimated block count: {estimatedTotalBlockCount}");
            Console.ReadLine();
        }

        private static double DecodeImageSharp(byte[] data)
        {
            var stream = new MemoryStream(data);
            Image img = new Image(stream);
            return img.Width * img.Height / 64.0;
        }

        private static double DecodeMonoSystemDrawing(byte[] data)
        {
            var stream = new MemoryStream(data);
            var img = new System.Drawing.Bitmap(stream);
            return img.Width * img.Height / 64.0;
        }

        private static double DecodeAll(byte[][] allData, int times, Func<byte[], double> decoderFunc)
        {
            

            double estimatedTotalBlockCount = 0;

            for (int i = 0; i < times; i++)
            {
                estimatedTotalBlockCount = 0;
                foreach (byte[] data in allData)
                {
                    estimatedTotalBlockCount += decoderFunc(data);
                }

            }
            
            return estimatedTotalBlockCount;
        }
    }
}

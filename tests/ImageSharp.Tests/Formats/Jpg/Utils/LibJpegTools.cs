namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Numerics;
    using System.Reflection;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;

    internal static partial class LibJpegTools
    {
        public static (double total, double average) CalculateDifference(ComponentData expected, ComponentData actual)
        {
            BigInteger totalDiff = 0;
            if (actual.WidthInBlocks < expected.WidthInBlocks)
            {
                throw new Exception("actual.WidthInBlocks < expected.WidthInBlocks");
            }

            if (actual.HeightInBlocks < expected.HeightInBlocks)
            {
                throw new Exception("actual.HeightInBlocks < expected.HeightInBlocks");
            }

            int w = expected.WidthInBlocks;
            int h = expected.HeightInBlocks;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Block8x8 aa = expected.SpectralBlocks[x, y];
                    Block8x8 bb = actual.SpectralBlocks[x, y];

                    long diff = Block8x8.TotalDifference(ref aa, ref bb);
                    totalDiff += diff;
                }
            }
            
            int count = w * h;
            double total = (double)totalDiff;
            double average = (double)totalDiff / (count * Block8x8.Size);
            return (total, average);
        }

        private static string DumpToolFullPath => Path.Combine(
            TestEnvironment.ToolsDirectoryFullPath,
            @"jpeg\dump-jpeg-coeffs.exe");

        public static void RunDumpJpegCoeffsTool(string sourceFile, string destFile)
        {
            string args = $@"""{sourceFile}"" ""{destFile}""";
            var process = Process.Start(DumpToolFullPath, args);
            process.WaitForExit();
        }

        public static SpectralData ExtractSpectralData(string inputFile)
        {
            TestFile testFile = TestFile.Create(inputFile);

            string outDir = TestEnvironment.CreateOutputDirectory(".Temp", $"JpegCoeffs");
            string fn = $"{Path.GetFileName(inputFile)}-{new Random().Next(1000)}.dctcoeffs";
            string coeffFileFullPath = Path.Combine(outDir, fn);

            try
            {
                RunDumpJpegCoeffsTool(testFile.FullPath, coeffFileFullPath);
                
                using (var dumpStream = new FileStream(coeffFileFullPath, FileMode.Open))
                using (var rdr = new BinaryReader(dumpStream))
                {
                    int componentCount = rdr.ReadInt16();
                    ComponentData[] result = new ComponentData[componentCount];

                    for (int i = 0; i < componentCount; i++)
                    {
                        int widthInBlocks = rdr.ReadInt16();
                        int heightInBlocks = rdr.ReadInt16();
                        ComponentData resultComponent = new ComponentData(heightInBlocks, widthInBlocks, i);
                        result[i] = resultComponent;
                    }

                    byte[] buffer = new byte[64*sizeof(short)];

                    for (int i = 0; i < result.Length; i++)
                    {
                        ComponentData c = result[i];

                        for (int y = 0; y < c.HeightInBlocks; y++)
                        {
                            for (int x = 0; x < c.WidthInBlocks; x++)
                            {
                                rdr.Read(buffer, 0, buffer.Length);

                                short[] block = buffer.AsSpan().NonPortableCast<byte, short>().ToArray();
                                c.MakeBlock(block, y, x);
                            }
                        }
                    }

                    return new SpectralData(result);
                }
            }
            finally
            {
                if (File.Exists(coeffFileFullPath))
                {
                    File.Delete(coeffFileFullPath);
                }
            }
        }
    }
}
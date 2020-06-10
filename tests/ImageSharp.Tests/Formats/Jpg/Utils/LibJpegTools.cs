// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    /// <summary>
    /// Utilities to read raw libjpeg data for reference conversion.
    /// </summary>
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

        /// <summary>
        /// Executes 'dump-jpeg-coeffs.exe' for the given jpeg image file, saving the libjpeg spectral data into 'destFile'. Windows only!
        /// See:
        /// <see>
        ///     <cref>https://github.com/SixLabors/Imagesharp.Tests.Images/blob/master/tools/jpeg/README.md</cref>
        /// </see>
        /// </summary>
        public static void RunDumpJpegCoeffsTool(string sourceFile, string destFile)
        {
            if (!TestEnvironment.IsWindows)
            {
                throw new InvalidOperationException("Can't run dump-jpeg-coeffs.exe in non-Windows environment. Skip this test on Linux/Unix!");
            }

            string args = $@"""{sourceFile}"" ""{destFile}""";
            var process = new Process
            {
                StartInfo =
                                      {
                                          FileName = DumpToolFullPath,
                                          Arguments = args,
                                          WindowStyle = ProcessWindowStyle.Hidden
                                      }
            };
            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// Extract libjpeg <see cref="SpectralData"/> from the given jpg file with  'dump-jpeg-coeffs.exe'. Windows only!
        /// See:
        /// https://github.com/SixLabors/Imagesharp.Tests.Images/blob/master/tools/jpeg/README.md
        /// </summary>
        public static SpectralData ExtractSpectralData(string inputFile)
        {
            TestFile testFile = TestFile.Create(inputFile);

            string outDir = TestEnvironment.CreateOutputDirectory(".Temp", "JpegCoeffs");
            string fn = $"{Path.GetFileName(inputFile)}-{new Random().Next(1000)}.dctcoeffs";
            string coeffFileFullPath = Path.Combine(outDir, fn);

            try
            {
                RunDumpJpegCoeffsTool(testFile.FullPath, coeffFileFullPath);

                using (var dumpStream = new FileStream(coeffFileFullPath, FileMode.Open))
                using (var rdr = new BinaryReader(dumpStream))
                {
                    int componentCount = rdr.ReadInt16();
                    var result = new ComponentData[componentCount];

                    for (int i = 0; i < componentCount; i++)
                    {
                        int widthInBlocks = rdr.ReadInt16();
                        int heightInBlocks = rdr.ReadInt16();
                        var resultComponent = new ComponentData(widthInBlocks, heightInBlocks, i);
                        result[i] = resultComponent;
                    }

                    var buffer = new byte[64 * sizeof(short)];

                    for (int i = 0; i < result.Length; i++)
                    {
                        ComponentData c = result[i];

                        for (int y = 0; y < c.HeightInBlocks; y++)
                        {
                            for (int x = 0; x < c.WidthInBlocks; x++)
                            {
                                rdr.Read(buffer, 0, buffer.Length);

                                short[] block = MemoryMarshal.Cast<byte, short>(buffer.AsSpan()).ToArray();
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

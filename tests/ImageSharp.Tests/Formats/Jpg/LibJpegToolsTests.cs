// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class LibJpegToolsTests
    {
        [Fact]
        public void RunDumpJpegCoeffsTool()
        {
            if (!TestEnvironment.IsWindows)
            {
                return;
            }

            string inputFile = TestFile.GetInputFileFullPath(TestImages.Jpeg.Progressive.Progress);
            string outputDir = TestEnvironment.CreateOutputDirectory(nameof(SpectralJpegTests));
            string outputFile = Path.Combine(outputDir, "progress.dctdump");

            LibJpegTools.RunDumpJpegCoeffsTool(inputFile, outputFile);

            Assert.True(File.Exists(outputFile));
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgba32)]
        [WithFile(TestImages.Jpeg.Progressive.Progress, PixelTypes.Rgba32)]
        public void ExtractSpectralData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.IsWindows)
            {
                return;
            }

            string testImage = provider.SourceFileOrDescription;
            LibJpegTools.SpectralData data = LibJpegTools.ExtractSpectralData(testImage);

            Assert.True(data.ComponentCount == 3);
            Assert.True(data.Components.Length == 3);

            VerifyJpeg.SaveSpectralImage(provider, data);

            // I knew this one well:
            if (testImage == TestImages.Jpeg.Progressive.Progress)
            {
                VerifyJpeg.VerifyComponentSizes3(data.Components, 43, 61, 22, 31, 22, 31);
            }
        }
    }
}

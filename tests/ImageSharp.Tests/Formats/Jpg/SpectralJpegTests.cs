// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class SpectralJpegTests
    {
        public SpectralJpegTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        public static readonly string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk, TestImages.Jpeg.Baseline.Jpeg400,
                TestImages.Jpeg.Baseline.Jpeg444, TestImages.Jpeg.Baseline.Testorig420,
                TestImages.Jpeg.Baseline.Jpeg420Small, TestImages.Jpeg.Baseline.Bad.BadEOF,
                TestImages.Jpeg.Baseline.MultiScanBaselineCMYK
            };

        public static readonly string[] ProgressiveTestJpegs =
            {
                TestImages.Jpeg.Progressive.Fb, TestImages.Jpeg.Progressive.Progress,
                TestImages.Jpeg.Progressive.Festzug, TestImages.Jpeg.Progressive.Bad.BadEOF,
                TestImages.Jpeg.Progressive.Bad.ExifUndefType,
            };

        public static readonly string[] AllTestJpegs = BaselineTestJpegs.Concat(ProgressiveTestJpegs).ToArray();

        [Theory(Skip = "Debug only, enable manually!")]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void Decoder_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using var ms = new MemoryStream(sourceBytes);
            using var bufferedStream = new BufferedReadStream(Configuration.Default, ms);
            decoder.ParseStream(bufferedStream);

            var data = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
            VerifyJpeg.SaveSpectralImage(provider, data);
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void VerifySpectralCorrectness<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.IsWindows)
            {
                return;
            }

            var decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using var ms = new MemoryStream(sourceBytes);
            using var bufferedStream = new BufferedReadStream(Configuration.Default, ms);
            decoder.ParseStream(bufferedStream);

            var imageSharpData = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
            this.VerifySpectralCorrectnessImpl(provider, imageSharpData);
        }

        private void VerifySpectralCorrectnessImpl<TPixel>(
            TestImageProvider<TPixel> provider,
            LibJpegTools.SpectralData imageSharpData)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            LibJpegTools.SpectralData libJpegData = LibJpegTools.ExtractSpectralData(provider.SourceFileOrDescription);

            bool equality = libJpegData.Equals(imageSharpData);
            this.Output.WriteLine("Spectral data equality: " + equality);

            int componentCount = imageSharpData.ComponentCount;
            if (libJpegData.ComponentCount != componentCount)
            {
                throw new Exception("libJpegData.ComponentCount != componentCount");
            }

            double averageDifference = 0;
            double totalDifference = 0;
            double tolerance = 0;

            this.Output.WriteLine("*** Differences ***");
            for (int i = 0; i < componentCount; i++)
            {
                LibJpegTools.ComponentData libJpegComponent = libJpegData.Components[i];
                LibJpegTools.ComponentData imageSharpComponent = imageSharpData.Components[i];

                (double total, double average) diff = LibJpegTools.CalculateDifference(libJpegComponent, imageSharpComponent);

                this.Output.WriteLine($"Component{i}: {diff}");
                averageDifference += diff.average;
                totalDifference += diff.total;
                tolerance += libJpegComponent.SpectralBlocks.GetSingleSpan().Length;
            }

            averageDifference /= componentCount;

            tolerance /= 64; // fair enough?

            this.Output.WriteLine($"AVERAGE: {averageDifference}");
            this.Output.WriteLine($"TOTAL: {totalDifference}");
            this.Output.WriteLine($"TOLERANCE = totalNumOfBlocks / 64 = {tolerance}");

            Assert.True(totalDifference < tolerance);
        }
    }
}

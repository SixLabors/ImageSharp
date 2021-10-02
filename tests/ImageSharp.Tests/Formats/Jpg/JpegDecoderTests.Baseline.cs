// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public partial class JpegDecoderTests
    {
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgb24, false)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, true)]
        [WithFile(TestImages.Jpeg.Baseline.Turtle420, PixelTypes.Rgb24, true)]
        public void DecodeBaselineJpeg<TPixel>(TestImageProvider<TPixel> provider, bool enforceDiscontiguousBuffers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            static void RunTest(string providerDump, string nonContiguousBuffersStr)
            {
                TestImageProvider<TPixel> provider =
                    BasicSerializer.Deserialize<TestImageProvider<TPixel>>(providerDump);

                if (!string.IsNullOrEmpty(nonContiguousBuffersStr))
                {
                    provider.LimitAllocatorBufferCapacity().InPixels(16_000);
                }

                using Image<TPixel> image = provider.GetImage(JpegDecoder);
                image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(
                    GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }

            string providerDump = BasicSerializer.Serialize(provider);
            RunTest(providerDump, enforceDiscontiguousBuffers ? "Disco" : string.Empty);

            // RemoteExecutor.Invoke(
            //         RunTest,
            //         providerDump,
            //         enforceDiscontiguousBuffers ? "Disco" : string.Empty)
            //     .Dispose();
        }

        [Theory]
        [WithFileCollection(nameof(UnrecoverableTestJpegs), PixelTypes.Rgba32)]
        public void UnrecoverableImage_Throws_InvalidImageContentException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<InvalidImageContentException>(provider.GetImage);
    }
}

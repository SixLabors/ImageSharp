// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class JpegDecoderTests
    {
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32, false)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgba32, true)]
        public void DecodeBaselineJpeg<TPixel>(TestImageProvider<TPixel> provider, bool enforceDiscontiguousBuffers)
            where TPixel : struct, IPixel<TPixel>
        {
            static void RunTest(string providerDump, string nonContiguousBuffersStr)
            {
                TestImageProvider<TPixel> provider =
                    BasicSerializer.Deserialize<TestImageProvider<TPixel>>(providerDump);

                if (!string.IsNullOrEmpty(nonContiguousBuffersStr))
                {
                    provider.LimitAllocatorBufferCapacity().InBytes(200);
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
            RemoteExecutor.Invoke(
                    RunTest,
                    providerDump,
                    enforceDiscontiguousBuffers ? "Disco" : string.Empty)
                .Dispose();
        }

        [Theory]
        [WithFileCollection(nameof(UnrecoverableTestJpegs), PixelTypes.Rgba32)]
        public void UnrecoverableImage_Throws_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel> => Assert.Throws<ImageFormatException>(provider.GetImage);
    }
}

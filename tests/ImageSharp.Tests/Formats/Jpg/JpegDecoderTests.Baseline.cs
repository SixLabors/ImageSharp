// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.


using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class JpegDecoderTests
    {
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            static void RunTest(string providerDump)
            {
                TestImageProvider<TPixel> provider =
                    BasicSerializer.Deserialize<TestImageProvider<TPixel>>(providerDump);

                using Image<TPixel> image = provider.GetImage(JpegDecoder);
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(
                    GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }

            string providerDump = BasicSerializer.Serialize(provider);
            RemoteExecutor.Invoke(RunTest, providerDump).Dispose();
        }

        [Theory]
        [WithFileCollection(nameof(UnrecoverableTestJpegs), PixelTypes.Rgba32)]
        public void UnrecoverableImagesShouldThrowCorrectError<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel> => Assert.Throws<ImageFormatException>(provider.GetImage);
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.


using SixLabors.ImageSharp.PixelFormats;
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
            if (SkipTest(provider))
            {
                // skipping to avoid OutOfMemoryException on CI
                return;
            }

            using (Image<TPixel> image = provider.GetImage(JpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(
                    this.GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFileCollection(nameof(UnrecoverableTestJpegs), PixelTypes.Rgba32)]
        public void UnrecoverableImagesShouldThrowCorrectError<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel> => Assert.Throws<ImageFormatException>(provider.GetImage);
    }
}

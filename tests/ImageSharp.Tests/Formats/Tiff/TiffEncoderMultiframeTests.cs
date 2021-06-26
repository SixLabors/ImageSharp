// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class TiffEncoderMultiframeTests : TiffEncoderBaseTester
    {
        [Theory]
        [WithFile(MultiframeLzwPredictor, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeMultiframe_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb);

        [Theory]
        [WithFile(MultiframeDeflateWithPreview, PixelTypes.Rgba32)]
        [WithFile(MultiframeDifferentSize, PixelTypes.Rgba32)]
        [WithFile(MultiframeDifferentVariants, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeMultiframe_NotSupport<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb));
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
using System;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.BigTiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Collection("RunSerial")]
    [Trait("Format", "Tiff")]
    [Trait("Format", "BigTiff")]
    public class BigTiffDecoderTests : TiffDecoderBaseTester
    {
        [Theory]
        [WithFile(BigTIFF, PixelTypes.Rgba32)]
        [WithFile(BigTIFFLong, PixelTypes.Rgba32)]
        [WithFile(BigTIFFLong8, PixelTypes.Rgba32)]
        [WithFile(BigTIFFMotorola, PixelTypes.Rgba32)]
        [WithFile(BigTIFFMotorolaLongStrips, PixelTypes.Rgba32)]
        [WithFile(BigTIFFSubIFD4, PixelTypes.Rgba32)]
        [WithFile(BigTIFFSubIFD8, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(BigTIFFLong8Tiles, PixelTypes.Rgba32)]
        public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => provider.GetImage(TiffDecoder));
    }
}

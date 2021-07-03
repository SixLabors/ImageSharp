// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    [Trait("Format", "Tiff.m")]
    public class TiffEncoderMultiframeTests : TiffEncoderBaseTester
    {
        [Theory]
        [WithFile(MultiframeLzwPredictor, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeMultiframe_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb);

        [Theory]
        [WithFile(MultiframeDifferentSize, PixelTypes.Rgba32)]
        [WithFile(MultiframeDifferentVariants, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeMultiframe_NotSupport<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb));

        [Theory]
        [WithFile(MultiframeDeflateWithPreview, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeMultiframe_WithoutPreview_ProblemTest<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<ImagesSimilarityException>(() => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb));

        [Theory]
        [WithFile(RgbLzwNoPredictor, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeMultiframe_Create<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var image = provider.GetImage();
            using var image2 = new Image<Rgba32>(image.Width, image.Height, Color.Green.ToRgba32());

            image.Frames.AddFrame(image2.Frames.RootFrame);

            TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Bit24;
            var encoder = new TiffEncoder
            {
                PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
                BitsPerPixel = bitsPerPixel,
                Compression = TiffCompression.Deflate
            };

            image.VerifyEncoder(
                provider,
                "tiff",
                bitsPerPixel,
                encoder,
                ImageComparer.Exact);
        }
    }
}

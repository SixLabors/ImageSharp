// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using static TestImages.Bmp;

    public class BmpDecoderTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes =
            PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        public static readonly string[] AllBmpFiles =
            {
                Car, F, NegHeight, CoreHeader, V5Header, RLE, RLEInverted, Bit8, Bit8Inverted, Bit16, Bit16Inverted
            };

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32)]
        public void DecodeBmp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider, "bmp");
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(F, CommonNonDefaultPixelTypes)]
        public void BmpDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider, "bmp");
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [InlineData(Car, 24)]
        [InlineData(F, 24)]
        [InlineData(NegHeight, 24)]
        [InlineData(Bit8, 8)]
        [InlineData(Bit8Inverted, 8)]
        public void DetectPixelSize(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.Identify(stream)?.PixelType?.BitsPerPixel);
            }
        }
    }
}
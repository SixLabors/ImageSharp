// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    using Xunit.Abstractions;

    public class BmpEncoderTests : FileTestBase
    {
        public static readonly TheoryData<BmpBitsPerPixel> BitsPerPixel =
            new TheoryData<BmpBitsPerPixel>
                {
                    BmpBitsPerPixel.Pixel24,
                    BmpBitsPerPixel.Pixel32
                };

        public BmpEncoderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel), 24, 24, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24)]
        public void Encode_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel>
        {
            TestBmpEncoderCore(provider, bitsPerPixel);
        }

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel), 48, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 47, 8, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 49, 7, PixelTypes.Rgba32)]
        [WithSolidFilledImages(nameof(BitsPerPixel), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 7, 5, PixelTypes.Rgba32)]
        public void Encode_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel>
        {
            TestBmpEncoderCore(provider, bitsPerPixel);
        }
        
        private static void TestBmpEncoderCore<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel>
        {   
            using (Image<TPixel> image = provider.GetImage())
            {
                // there is no alpha in bmp!
                image.Mutate(c => c.MakeOpaque());

                var encoder = new BmpEncoder { BitsPerPixel = bitsPerPixel };

                // Does DebugSave & load reference CompareToReferenceInput():
                image.VerifyEncoder(provider, "bmp", bitsPerPixel, encoder);
            }
        }
    }
}
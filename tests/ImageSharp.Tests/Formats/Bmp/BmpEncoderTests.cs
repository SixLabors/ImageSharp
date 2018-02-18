// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    using Xunit.Abstractions;

    public class BmpEncoderTests : FileTestBase
    {
        private const PixelTypes PixelTypesToTest = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24;

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
        [WithTestPatternImages(nameof(BitsPerPixel), 48, 24, PixelTypesToTest)]
        [WithTestPatternImages(nameof(BitsPerPixel), 47, 8, PixelTypesToTest)]
        [WithTestPatternImages(nameof(BitsPerPixel), 49, 7, PixelTypesToTest)]
        [WithSolidFilledImages(nameof(BitsPerPixel), 1, 1, 255, 100, 50, 255, PixelTypesToTest)]
        [WithTestPatternImages(nameof(BitsPerPixel), 7, 5, PixelTypesToTest)]
        public void Encode<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel>
        {
            
            using (Image<TPixel> image = provider.GetImage())
            {
                // there is no alpha in bmp!
                image.Mutate(
                    c => c.Opacity(1)
                    );
                
                var encoder = new BmpEncoder { BitsPerPixel = bitsPerPixel };
                string path = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, testOutputDetails:bitsPerPixel);

                this.Output.WriteLine(path);

                IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(path);
                string referenceOutputFile = provider.Utility.GetReferenceOutputFileName("bmp", bitsPerPixel, true);

                this.Output.WriteLine(referenceOutputFile);

                //using (var encodedImage = Image.Load<TPixel>(referenceOutputFile, referenceDecoder))
                //{
                //    ImageComparer.Exact.CompareImagesOrFrames(image, encodedImage);
                //}
            }
        }
    }
}
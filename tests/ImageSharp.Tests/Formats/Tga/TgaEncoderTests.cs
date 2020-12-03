// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tga;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    [Trait("Format", "Tga")]
    public class TgaEncoderTests
    {
        public static readonly TheoryData<TgaBitsPerPixel> BitsPerPixel =
            new TheoryData<TgaBitsPerPixel>
            {
                TgaBitsPerPixel.Pixel24,
                TgaBitsPerPixel.Pixel32
            };

        public static readonly TheoryData<string, TgaBitsPerPixel> TgaBitsPerPixelFiles =
            new TheoryData<string, TgaBitsPerPixel>
            {
                { Gray8BitBottomLeft, TgaBitsPerPixel.Pixel8 },
                { Bit16BottomLeft, TgaBitsPerPixel.Pixel16 },
                { Bit24BottomLeft, TgaBitsPerPixel.Pixel24 },
                { Bit32BottomLeft, TgaBitsPerPixel.Pixel32 },
            };

        [Theory]
        [MemberData(nameof(TgaBitsPerPixelFiles))]
        public void TgaEncoder_PreserveBitsPerPixel(string imagePath, TgaBitsPerPixel bmpBitsPerPixel)
        {
            var options = new TgaEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);
                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        TgaMetadata meta = output.Metadata.GetTgaMetadata();
                        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(TgaBitsPerPixelFiles))]
        public void TgaEncoder_WithCompression_PreserveBitsPerPixel(string imagePath, TgaBitsPerPixel bmpBitsPerPixel)
        {
            var options = new TgaEncoder()
                          {
                              Compression = TgaCompression.RunLength
                          };

            TestFile testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);
                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        TgaMetadata meta = output.Metadata.GetTgaMetadata();
                        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                    }
                }
            }
        }

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit8_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel8)

            // Using tolerant comparer here. The results from magick differ slightly. Maybe a different ToGrey method is used. The image looks otherwise ok.
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None, useExactComparer: false, compareTolerance: 0.03f);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit16_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel16)
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None, useExactComparer: false);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit24_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel24)
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit32_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel32)
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit8_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel8)

            // Using tolerant comparer here. The results from magick differ slightly. Maybe a different ToGrey method is used. The image looks otherwise ok.
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength, useExactComparer: false, compareTolerance: 0.03f);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit16_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel16)
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength, useExactComparer: false);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit24_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel24)
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaEncoder_Bit32_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Pixel32)
            where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength);

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32, TgaBitsPerPixel.Pixel32)]
        [WithFile(Bit24BottomLeft, PixelTypes.Rgba32, TgaBitsPerPixel.Pixel24)]
        public void TgaEncoder_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);
            TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength);
        }

        private static void TestTgaEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            TgaBitsPerPixel bitsPerPixel,
            TgaCompression compression = TgaCompression.None,
            bool useExactComparer = true,
            float compareTolerance = 0.01f)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new TgaEncoder { BitsPerPixel = bitsPerPixel, Compression = compression };

                using (var memStream = new MemoryStream())
                {
                    image.Save(memStream, encoder);
                    memStream.Position = 0;
                    using (var encodedImage = (Image<TPixel>)Image.Load(memStream))
                    {
                        TgaTestUtils.CompareWithReferenceDecoder(provider, encodedImage, useExactComparer, compareTolerance);
                    }
                }
            }
        }
    }
}

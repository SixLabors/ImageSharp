// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.DotNet.RemoteExecutor;

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    using static TestImages.Tga;

    public class TgaDecoderTests
    {
        private static TgaDecoder TgaDecoder => new TgaDecoder();

        [Theory]
        [WithFile(Gray8Bit, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray16Bit, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Gray16BitBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Gray16BitBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_WithBottomRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Gray16BitRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Gray16BitRleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Gray16BitRleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Gray16BitRleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithTopRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);

                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder output seems not to be correct for 16bit gray images.
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Bit15, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit15Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16PalRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24BottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Palette_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32BottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32TopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32RleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32RleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(NoAlphaBits16Bit, PixelTypes.Rgba32)]
        [WithFile(NoAlphaBits16BitRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WhenAlphaBitsNotSet_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(NoAlphaBits32Bit, PixelTypes.Rgba32)]
        [WithFile(NoAlphaBits32BitRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WhenAlphaBitsNotSet<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder does not ignore the alpha data here.
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(Bit16, PixelTypes.Rgba32)]
        [WithFile(Bit24, PixelTypes.Rgba32)]
        [WithFile(Bit32, PixelTypes.Rgba32)]
        public void TgaDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
            ImageFormatException ex = Assert.Throws<ImageFormatException>(() => provider.GetImage(TgaDecoder));
            Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
        }

        [Theory]
        [WithFile(Bit24, PixelTypes.Rgba32)]
        [WithFile(Bit32, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithLimitedAllocatorBufferCapacity<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            static void RunTest(string providerDump, string nonContiguousBuffersStr)
            {
                TestImageProvider<TPixel> provider = BasicSerializer.Deserialize<TestImageProvider<TPixel>>(providerDump);

                provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

                using Image<TPixel> image = provider.GetImage(TgaDecoder);
                image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr);

                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }

            string providerDump = BasicSerializer.Serialize(provider);
            RemoteExecutor.Invoke(
                    RunTest,
                    providerDump,
                    "Disco")
                .Dispose();
        }
    }
}

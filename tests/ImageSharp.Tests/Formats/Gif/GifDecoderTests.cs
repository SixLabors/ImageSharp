// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using Microsoft.DotNet.RemoteExecutor;

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    [Trait("Format", "Gif")]
    [ValidateDisposedMemoryAllocations]
    public class GifDecoderTests
    {
        private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;

        private static GifDecoder GifDecoder => new();

        public static readonly string[] MultiFrameTestFiles =
        {
            TestImages.Gif.Giphy, TestImages.Gif.Kumin
        };

        [Theory]
        [WithFileCollection(nameof(MultiFrameTestFiles), PixelTypes.Rgba32)]
        public void Decode_VerifyAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSaveMultiFrame(provider);
                image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
            }
        }

        [Fact]
        public unsafe void Decode_NonTerminatedFinalFrame()
        {
            var testFile = TestFile.Create(TestImages.Gif.Rings);

            int length = testFile.Bytes.Length - 2;

            fixed (byte* data = testFile.Bytes.AsSpan(0, length))
            {
                using (var stream = new UnmanagedMemoryStream(data, length))
                {
                    using (Image<Rgba32> image = GifDecoder.Decode<Rgba32>(Configuration.Default, stream, default))
                    {
                        Assert.Equal((200, 200), (image.Width, image.Height));
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Trans, TestPixelTypes)]
        public void GifDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider);
                image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Cheers, PixelTypes.Rgba32, 93)]
        [WithFile(TestImages.Gif.Rings, PixelTypes.Rgba32, 1)]
        [WithFile(TestImages.Gif.Issues.BadDescriptorWidth, PixelTypes.Rgba32, 36)]
        public void Decode_VerifyRootFrameAndFrameCount<TPixel>(TestImageProvider<TPixel> provider, int expectedFrameCount)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.Equal(expectedFrameCount, image.Frames.Count);
                image.DebugSave(provider);
                image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        public void CanDecodeJustOneFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new GifDecoder { DecodingMode = FrameDecodingMode.First }))
            {
                Assert.Equal(1, image.Frames.Count);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        public void CanDecodeAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new GifDecoder { DecodingMode = FrameDecodingMode.All }))
            {
                Assert.True(image.Frames.Count > 1);
            }
        }

        [Theory]
        [InlineData(TestImages.Gif.Cheers, 8)]
        [InlineData(TestImages.Gif.Giphy, 8)]
        [InlineData(TestImages.Gif.Rings, 8)]
        [InlineData(TestImages.Gif.Trans, 8)]
        public void DetectPixelSize(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.Identify(stream)?.PixelType?.BitsPerPixel);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.ZeroSize, PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.ZeroWidth, PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.ZeroHeight, PixelTypes.Rgba32)]
        public void Decode_WithInvalidDimensions_DoesThrowException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using Image<TPixel> image = provider.GetImage(GifDecoder);
                });
            Assert.NotNull(ex);
            Assert.Contains("Width or height should not be 0", ex.Message);
        }

        [Theory]
        [WithFile(TestImages.Gif.MaxWidth, PixelTypes.Rgba32, 65535, 1)]
        [WithFile(TestImages.Gif.MaxHeight, PixelTypes.Rgba32, 1, 65535)]
        public void Decode_WithMaxDimensions_Works<TPixel>(TestImageProvider<TPixel> provider, int expectedWidth, int expectedHeight)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(GifDecoder))
            {
                Assert.Equal(expectedWidth, image.Width);
                Assert.Equal(expectedHeight, image.Height);
            }
        }

        [Fact]
        public void CanDecodeIntermingledImages()
        {
            using (var kumin1 = Image.Load<Rgba32>(TestFile.Create(TestImages.Gif.Kumin).Bytes))
            using (Image.Load(TestFile.Create(TestImages.Png.Icon).Bytes))
            using (var kumin2 = Image.Load<Rgba32>(TestFile.Create(TestImages.Gif.Kumin).Bytes))
            {
                for (int i = 0; i < kumin1.Frames.Count; i++)
                {
                    ImageFrame<Rgba32> first = kumin1.Frames[i];
                    ImageFrame<Rgba32> second = kumin2.Frames[i];

                    Assert.True(second.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> secondMemory));

                    first.ComparePixelBufferTo(secondMemory.Span);
                }
            }
        }

        // https://github.com/SixLabors/ImageSharp/issues/1503
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue1530, PixelTypes.Rgba32)]
        public void Issue1530_BadDescriptorDimensions<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSaveMultiFrame(provider);
            image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
        }

        // https://github.com/SixLabors/ImageSharp/issues/2758
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue2758, PixelTypes.Rgba32)]
        public void Issue2758_BadDescriptorDimensions<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSaveMultiFrame(provider);
            image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
        }

        // https://github.com/SixLabors/ImageSharp/issues/405
        [Theory]
        [WithFile(TestImages.Gif.Issues.BadAppExtLength, PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.Issues.BadAppExtLength_2, PixelTypes.Rgba32)]
        public void Issue405_BadApplicationExtensionBlockLength<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider);

                image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        // https://github.com/SixLabors/ImageSharp/issues/1668
        [Theory]
        [WithFile(TestImages.Gif.Issues.InvalidColorIndex, PixelTypes.Rgba32)]
        public void Issue1668_InvalidColorIndex<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider);

                image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.Kumin, PixelTypes.Rgba32)]
        public void GifDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
            InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(GifDecoder));
            Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.Kumin, PixelTypes.Rgba32)]
        public void GifDecoder_CanDecode_WithLimitedAllocatorBufferCapacity(
            TestImageProvider<Rgba32> provider)
        {
            static void RunTest(string providerDump, string nonContiguousBuffersStr)
            {
                TestImageProvider<Rgba32> provider
                    = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

                provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

                using Image<Rgba32> image = provider.GetImage(GifDecoder);
                image.DebugSave(provider, nonContiguousBuffersStr);
                image.CompareToOriginal(provider);
            }

            string providerDump = BasicSerializer.Serialize(provider);
            RemoteExecutor.Invoke(
                    RunTest,
                    providerDump,
                    "Disco")
                .Dispose();
        }

        // https://github.com/SixLabors/ImageSharp/issues/1962
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue1962NoColorTable, PixelTypes.Rgba32)]
        public void Issue1962<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider);

            image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
        }

        // https://github.com/SixLabors/ImageSharp/issues/2012
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue2012EmptyXmp, PixelTypes.Rgba32)]
        public void Issue2012EmptyXmp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            image.DebugSave(provider);
            image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
        }

        // https://github.com/SixLabors/ImageSharp/issues/2012
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue2012BadMinCode, PixelTypes.Rgba32)]
        public void Issue2012BadMinCode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider);
            image.CompareToReferenceOutput(provider);
        }

        // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
        [Theory]
        [WithFile(TestImages.Gif.Issues.DeferredClearCode, PixelTypes.Rgba32)]
        public void IssueDeferredClearCode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            image.DebugSave(provider);
            image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
        }

        // https://github.com/SixLabors/ImageSharp/issues/2743
        [Theory]
        [WithFile(TestImages.Gif.Issues.BadMaxLzwBits, PixelTypes.Rgba32)]
        public void IssueTooLargeLzwBits<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSaveMultiFrame(provider);
            image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
        }

        // https://github.com/SixLabors/ImageSharp/issues/2859
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue2859_A, PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.Issues.Issue2859_B, PixelTypes.Rgba32)]
        public void Issue2859_LZWPixelStackOverflow<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSaveMultiFrame(provider);
            image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
        }

        // https://github.com/SixLabors/ImageSharp/issues/2953
        [Theory]
        [WithFile(TestImages.Gif.Issues.Issue2953, PixelTypes.Rgba32)]
        public void Issue2953<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // We should throw a InvalidImageContentException when trying to identify or load an invalid GIF file.
            var testFile = TestFile.Create(provider.SourceFileOrDescription);

            Assert.Throws<InvalidImageContentException>(() => Image.Identify(testFile.FullPath));
            Assert.Throws<InvalidImageContentException>(() => Image.Load(testFile.FullPath));
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifDecoderTests
    {
        private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;

        public static readonly string[] MultiFrameTestFiles =
        {
            TestImages.Gif.Giphy, TestImages.Gif.Kumin
        };

        public static readonly string[] BasicVerificationFiles =
        {
            TestImages.Gif.Cheers,
            TestImages.Gif.Rings,

            // previously DecodeBadApplicationExtensionLength:
            TestImages.Gif.Issues.BadAppExtLength,
            TestImages.Gif.Issues.BadAppExtLength_2,

            // previously DecodeBadDescriptorDimensionsLength:
            TestImages.Gif.Issues.BadDescriptorWidth
        };

        private static readonly Dictionary<string, int> BasicVerificationFrameCount =
        new Dictionary<string, int>
        {
            [TestImages.Gif.Cheers] = 93,
            [TestImages.Gif.Issues.BadDescriptorWidth] = 36,
        };

        public static readonly string[] BadAppExtFiles =
        {
            TestImages.Gif.Issues.BadAppExtLength,
            TestImages.Gif.Issues.BadAppExtLength_2
        };

        [Theory]
        [WithFileCollection(nameof(MultiFrameTestFiles), PixelTypes.Rgba32)]
        public void Decode_VerifyAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
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
                    var decoder = new GifDecoder();

                    using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, stream))
                    {
                        Assert.Equal((200, 200), (image.Width, image.Height));
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Trans, TestPixelTypes)]
        public void GifDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider);
                image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(BasicVerificationFiles), PixelTypes.Rgba32)]
        public void Decode_VerifyRootFrameAndFrameCount<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (!BasicVerificationFrameCount.TryGetValue(provider.SourceFileOrDescription, out int expectedFrameCount))
            {
                expectedFrameCount = 1;
            }

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
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new GifDecoder { DecodingMode = FrameDecodingMode.First }))
            {
                Assert.Equal(1, image.Frames.Count);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        public void CanDecodeAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
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

        [Fact]
        public void CanDecodeIntermingledImages()
        {
            using (var kumin1 = Image.Load(TestFile.Create(TestImages.Gif.Kumin).Bytes))
            using (Image.Load(TestFile.Create(TestImages.Png.Icon).Bytes))
            using (var kumin2 = Image.Load(TestFile.Create(TestImages.Gif.Kumin).Bytes))
            {
                for (int i = 0; i < kumin1.Frames.Count; i++)
                {
                    ImageFrame<Rgba32> first = kumin1.Frames[i];
                    ImageFrame<Rgba32> second = kumin2.Frames[i];
                    first.ComparePixelBufferTo(second.GetPixelSpan());
                }
            }
        }
    }
}

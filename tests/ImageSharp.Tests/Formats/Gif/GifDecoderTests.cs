// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Advanced;

    public class GifDecoderTests
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32 | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;

        public static readonly string[] TestFiles = { TestImages.Gif.Giphy, TestImages.Gif.Rings, TestImages.Gif.Trans };

        public static readonly string[] BadAppExtFiles = { TestImages.Gif.Issues.BadAppExtLength, TestImages.Gif.Issues.BadAppExtLength_2 };

        [Theory]
        [WithFileCollection(nameof(TestFiles), PixelTypes)]
        public void DecodeAndReSave<TPixel>(TestImageProvider<TPixel> imageProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = imageProvider.GetImage())
            {
                imageProvider.Utility.SaveTestOutputFile(image, "bmp");
                imageProvider.Utility.SaveTestOutputFile(image, "gif");
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestFiles), PixelTypes)]
        public void DecodeResizeAndSave<TPixel>(TestImageProvider<TPixel> imageProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = imageProvider.GetImage())
            {
                image.Mutate(x => x.Resize(new Size(image.Width / 2, image.Height / 2)));

                imageProvider.Utility.SaveTestOutputFile(image, "bmp");
                imageProvider.Utility.SaveTestOutputFile(image, "gif");
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsFalse_CommentsAreRead()
        {
            var options = new GifDecoder
            {
                IgnoreMetadata = false
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(1, image.MetaData.Properties.Count);
                Assert.Equal("Comments", image.MetaData.Properties[0].Name);
                Assert.Equal("ImageSharp", image.MetaData.Properties[0].Value);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsTrue_CommentsAreIgnored()
        {
            var options = new GifDecoder
            {
                IgnoreMetadata = true
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(0, image.MetaData.Properties.Count);
            }
        }

        [Fact]
        public void Decode_TextEncodingSetToUnicode_TextIsReadWithCorrectEncoding()
        {
            var options = new GifDecoder
            {
                TextEncoding = Encoding.Unicode
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(1, image.MetaData.Properties.Count);
                Assert.Equal("浉条卥慨灲", image.MetaData.Properties[0].Value);
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

        [Fact]
        public void CanDecodeIntermingledImages()
        {
            using (var kumin1 = Image.Load(TestFile.Create(TestImages.Gif.Kumin).Bytes))
            using (var icon = Image.Load(TestFile.Create(TestImages.Png.Icon).Bytes))
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

        [Theory]
        [WithFileCollection(nameof(BadAppExtFiles), PixelTypes.Rgba32)]
        public void DecodeBadApplicationExtensionLength<TPixel>(TestImageProvider<TPixel> imageProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = imageProvider.GetImage())
            {
                imageProvider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Issues.BadDescriptorWidth, PixelTypes.Rgba32)]
        public void DecodeBadDescriptorDimensionsLength<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }
    }
}
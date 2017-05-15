// <copyright file="GifDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System.Text;
    using Xunit;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    public class GifDecoderTests
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.StandardImageClass | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;

        public static readonly string[] TestFiles = { TestImages.Gif.Giphy, TestImages.Gif.Rings, TestImages.Gif.Trans };

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
        
        [Fact]
        public void Decode_IgnoreMetadataIsFalse_CommentsAreRead()
        {
            DecoderOptions options = new DecoderOptions()
            {
                IgnoreMetadata = false
            };

            TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

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
            DecoderOptions options = new DecoderOptions()
            {
                IgnoreMetadata = true
            };

            TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(0, image.MetaData.Properties.Count);
            }
        }

        [Fact]
        public void Decode_TextEncodingSetToUnicode_TextIsReadWithCorrectEncoding()
        {
            GifDecoderOptions options = new GifDecoderOptions()
            {
                TextEncoding = Encoding.Unicode
            };

            TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(1, image.MetaData.Properties.Count);
                Assert.Equal("浉条卥慨灲", image.MetaData.Properties[0].Value);
            }
        }
    }
}

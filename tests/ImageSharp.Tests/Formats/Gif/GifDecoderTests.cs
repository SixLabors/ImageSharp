// <copyright file="GifDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Text;
    using Xunit;

    using ImageSharp.Formats;

    public class GifDecoderTests
    {
        [Fact]
        public void Decode_IgnoreMetadataIsFalse_CommentsAreRead()
        {
            DecoderOptions options = new DecoderOptions()
            {
                IgnoreMetadata = false
            };

            TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image image = testFile.CreateImage(options))
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

            using (Image image = testFile.CreateImage(options))
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

            using (Image image = testFile.CreateImage(options))
            {
                Assert.Equal(1, image.MetaData.Properties.Count);
                Assert.Equal("浉条卥慨灲", image.MetaData.Properties[0].Value);
            }
        }
    }
}

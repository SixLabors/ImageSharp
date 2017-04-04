// <copyright file="GifEncoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using Xunit;

    using ImageSharp.Formats;

    public class GifEncoderTests
    {
        [Fact]
        public void Encode_IgnoreMetadataIsFalse_CommentsAreWritten()
        {
            EncoderOptions options = new EncoderOptions()
            {
                IgnoreMetadata = false
            };

            TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image input = testFile.CreateImage())
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    input.Save(memStream, new GifFormat(), options);

                    memStream.Position = 0;
                    using (Image output = Image.Load(memStream))
                    {
                        Assert.Equal(1, output.MetaData.Properties.Count);
                        Assert.Equal("Comments", output.MetaData.Properties[0].Name);
                        Assert.Equal("ImageSharp", output.MetaData.Properties[0].Value);
                    }
                }
            }
        }

        [Fact]
        public void Encode_IgnoreMetadataIsTrue_CommentsAreNotWritten()
        {
            GifEncoderOptions options = new GifEncoderOptions()
            {
                IgnoreMetadata = true
            };

            TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image input = testFile.CreateImage())
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    input.SaveAsGif(memStream, options);

                    memStream.Position = 0;
                    using (Image output = Image.Load(memStream))
                    {
                        Assert.Equal(0, output.MetaData.Properties.Count);
                    }
                }
            }
        }

        [Fact]
        public void Encode_CommentIsToLong_CommentIsTrimmed()
        {
            using (Image input = new Image(1, 1))
            {
                string comments = new string('c', 256);
                input.MetaData.Properties.Add(new ImageProperty("Comments", comments));

                using (MemoryStream memStream = new MemoryStream())
                {
                    input.Save(memStream, new GifFormat());

                    memStream.Position = 0;
                    using (Image output = Image.Load(memStream))
                    {
                        Assert.Equal(1, output.MetaData.Properties.Count);
                        Assert.Equal("Comments", output.MetaData.Properties[0].Name);
                        Assert.Equal(255, output.MetaData.Properties[0].Value.Length);
                    }
                }
            }
        }
    }
}

// <copyright file="JpegDecoderCoreTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using Xunit;

    public class JpegDecoderCoreTests
    {
      [Fact]
      public void Decode_IgnoreMetadataIsFalse_ExifProfileIsRead()
      {
          var options = new DecoderOptions()
          {
              IgnoreMetadata = false
          };

          TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

          using (Image image = testFile.CreateImage(options))
          {
              Assert.NotNull(image.MetaData.ExifProfile);
          }
      }

      [Fact]
      public void Decode_IgnoreMetadataIsTrue_ExifProfileIgnored()
      {
          var options = new DecoderOptions()
          {
              IgnoreMetadata = true
          };

          TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

          using (Image image = testFile.CreateImage(options))
          {
              Assert.Null(image.MetaData.ExifProfile);
          }
      }
    }
}
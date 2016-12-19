// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
  using System;

  using Xunit;

  /// <summary>
  /// Tests the <see cref="Image{TPacked,TPixel}"/> class.
  /// </summary>
  public class ImageTests
  {
    [Fact]
    public void ConstructorByteArray()
    {
      Assert.Throws<ArgumentNullException>(() =>
      {
        new Image((byte[])null);
      });

      TestFile file = TestImages.Bmp.Car;
      var image = new Image<Color, uint>(file.Bytes);

      Assert.Equal(600, image.Width);
      Assert.Equal(450, image.Height);
    }
  }
}

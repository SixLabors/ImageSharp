// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
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

            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            Image image = new Image(file.Bytes);

            Assert.Equal(600, image.Width);
            Assert.Equal(450, image.Height);
        }
    }
}

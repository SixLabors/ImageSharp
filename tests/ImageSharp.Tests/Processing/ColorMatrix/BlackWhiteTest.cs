// <copyright file="BlackWhiteTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class BlackWhiteTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BlackWhite_CorrectProcessor()
        {
            this.operations.BlackWhite();
            var p = this.Verify<BlackWhiteProcessor<Rgba32>>();
        }

        [Fact]
        public void BlackWhite_rect_CorrectProcessor()
        {
            this.operations.BlackWhite( this.rect);
            var p = this.Verify<BlackWhiteProcessor<Rgba32>>(this.rect);
        }
    }
}
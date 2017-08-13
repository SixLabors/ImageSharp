// <copyright file="GrayscaleTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using System.Collections;
    using System.Collections.Generic;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using ImageSharp.Processing.Processors;
    using ImageSharp.Tests.TestUtilities;
    using SixLabors.Primitives;
    using Xunit;

    public class GrayscaleTest : BaseImageOperationsExtensionTest
    {
        public static IEnumerable<object[]> ModeTheoryData = new[] {
            new object[]{ new TestType<GrayscaleBt709Processor<Rgba32>>(), GrayscaleMode.Bt709 }
        };

        [Theory]
        [MemberData(nameof(ModeTheoryData))]
        public void Grayscale_mode_CorrectProcessor<T>(TestType<T> testType, GrayscaleMode mode)
            where T : IImageProcessor<Rgba32>
        {
            this.operations.Grayscale(mode);
            var p = this.Verify<T>();

        }

        [Theory]
        [MemberData(nameof(ModeTheoryData))]
        public void Grayscale_mode_rect_CorrectProcessor<T>(TestType<T> testType, GrayscaleMode mode)
            where T : IImageProcessor<Rgba32>
        {
            this.operations.Grayscale(mode, this.rect);
            this.Verify<T>(this.rect);
        }

        [Fact]
        public void Grayscale_rect_CorrectProcessor()
        {
            this.operations.Grayscale(this.rect);
            this.Verify<GrayscaleBt709Processor<Rgba32>>(this.rect);
        }
    }
}
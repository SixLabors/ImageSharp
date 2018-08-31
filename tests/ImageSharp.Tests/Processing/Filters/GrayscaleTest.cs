// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Tests.TestUtilities;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
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
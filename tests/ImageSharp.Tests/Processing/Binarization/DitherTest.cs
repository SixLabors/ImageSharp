// <copyright file="DitherTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Binarization
{
    using ImageSharp.Dithering;
    using ImageSharp.Dithering.Ordered;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using Moq;
    using SixLabors.Primitives;
    using Xunit;

    public class DitherTest : BaseImageOperationsExtensionTest
    {
        private readonly IOrderedDither orderedDither;
        private readonly IErrorDiffuser errorDiffuser;

        public DitherTest()
        {
            this.orderedDither = new Mock<IOrderedDither>().Object;
            this.errorDiffuser = new Mock<IErrorDiffuser>().Object;
        }
        [Fact]
        public void Dither_CorrectProcessor()
        {
            this.operations.Dither(orderedDither);
            var p = this.Verify<OrderedDitherProcessor<Rgba32>>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(0, p.Index);
        }

        [Fact]
        public void Dither_rect_CorrectProcessor()
        {
            this.operations.Dither(orderedDither, this.rect);
            var p = this.Verify<OrderedDitherProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(0, p.Index);
        }
        [Fact]
        public void Dither_index_CorrectProcessor()
        {
            this.operations.Dither(orderedDither, 2);
            var p = this.Verify<OrderedDitherProcessor<Rgba32>>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(2, p.Index);
        }

        [Fact]
        public void Dither_index_rect_CorrectProcessor()
        {
            this.operations.Dither(orderedDither, this.rect, 2);
            var p = this.Verify<OrderedDitherProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(2, p.Index);
        }


        [Fact]
        public void Dither_ErrorDifuser_CorrectProcessor()
        {
            this.operations.Dither(errorDiffuser, 4);
            var p = this.Verify<ErrorDiffusionDitherProcessor<Rgba32>>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(4, p.Threshold);
        }

        [Fact]
        public void Dither_ErrorDifuser_rect_CorrectProcessor()
        {
            this.operations.Dither(this.errorDiffuser, 3, this.rect);
            var p = this.Verify<ErrorDiffusionDitherProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(3, p.Threshold);
        }
    }
}
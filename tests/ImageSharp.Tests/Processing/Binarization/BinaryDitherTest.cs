// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class BinaryDitherTest : BaseImageOperationsExtensionTest
    {
        private readonly IOrderedDither orderedDither;
        private readonly IErrorDiffuser errorDiffuser;

        public BinaryDitherTest()
        {
            this.orderedDither = KnownDitherers.BayerDither4x4;
            this.errorDiffuser = KnownDiffusers.FloydSteinberg;
        }

        [Fact]
        public void BinaryDither_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither);
            BinaryOrderedDitherProcessor<Rgba32> p = this.Verify<BinaryOrderedDitherProcessor<Rgba32>>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(NamedColors<Rgba32>.White, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_rect_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither, this.rect);
            BinaryOrderedDitherProcessor<Rgba32> p = this.Verify<BinaryOrderedDitherProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(NamedColors<Rgba32>.White, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Black, p.LowerColor);
        }
        [Fact]
        public void BinaryDither_index_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither, NamedColors<Rgba32>.Yellow, NamedColors<Rgba32>.HotPink);
            BinaryOrderedDitherProcessor<Rgba32> p = this.Verify<BinaryOrderedDitherProcessor<Rgba32>>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(NamedColors<Rgba32>.Yellow, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.HotPink, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_index_rect_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither, NamedColors<Rgba32>.Yellow, NamedColors<Rgba32>.HotPink, this.rect);
            BinaryOrderedDitherProcessor<Rgba32> p = this.Verify<BinaryOrderedDitherProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(NamedColors<Rgba32>.HotPink, p.LowerColor);
        }


        [Fact]
        public void BinaryDither_ErrorDiffuser_CorrectProcessor()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .4F);
            BinaryErrorDiffusionProcessor<Rgba32> p = this.Verify<BinaryErrorDiffusionProcessor<Rgba32>>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.4F, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.White, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_ErrorDiffuser_rect_CorrectProcessor()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .3F, this.rect);
            BinaryErrorDiffusionProcessor<Rgba32> p = this.Verify<BinaryErrorDiffusionProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.3F, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.White, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_ErrorDiffuser_CorrectProcessorWithColors()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .5F, NamedColors<Rgba32>.HotPink, NamedColors<Rgba32>.Yellow);
            BinaryErrorDiffusionProcessor<Rgba32> p = this.Verify<BinaryErrorDiffusionProcessor<Rgba32>>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.HotPink, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_ErrorDiffuser_rect_CorrectProcessorWithColors()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .5F, NamedColors<Rgba32>.HotPink, NamedColors<Rgba32>.Yellow, this.rect);
            BinaryErrorDiffusionProcessor<Rgba32> p = this.Verify<BinaryErrorDiffusionProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.HotPink, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Yellow, p.LowerColor);
        }
    }
}
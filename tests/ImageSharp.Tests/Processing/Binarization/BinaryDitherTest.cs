// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
            BinaryOrderedDitherProcessor p = this.Verify<BinaryOrderedDitherProcessor>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_rect_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither, this.rect);
            BinaryOrderedDitherProcessor p = this.Verify<BinaryOrderedDitherProcessor>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }
        [Fact]
        public void BinaryDither_index_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither, Color.Yellow, Color.HotPink);
            BinaryOrderedDitherProcessor p = this.Verify<BinaryOrderedDitherProcessor>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.Yellow, p.UpperColor);
            Assert.Equal(Color.HotPink, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_index_rect_CorrectProcessor()
        {
            this.operations.BinaryDither(this.orderedDither, Color.Yellow, Color.HotPink, this.rect);
            BinaryOrderedDitherProcessor p = this.Verify<BinaryOrderedDitherProcessor>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.HotPink, p.LowerColor);
        }


        [Fact]
        public void BinaryDither_ErrorDiffuser_CorrectProcessor()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .4F);
            BinaryErrorDiffusionProcessor p = this.Verify<BinaryErrorDiffusionProcessor>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.4F, p.Threshold);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_ErrorDiffuser_rect_CorrectProcessor()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .3F, this.rect);
            BinaryErrorDiffusionProcessor p = this.Verify<BinaryErrorDiffusionProcessor>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.3F, p.Threshold);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_ErrorDiffuser_CorrectProcessorWithColors()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .5F, Color.HotPink, Color.Yellow);
            BinaryErrorDiffusionProcessor p = this.Verify<BinaryErrorDiffusionProcessor>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryDither_ErrorDiffuser_rect_CorrectProcessorWithColors()
        {
            this.operations.BinaryDiffuse(this.errorDiffuser, .5F, Color.HotPink, Color.Yellow, this.rect);
            BinaryErrorDiffusionProcessor p = this.Verify<BinaryErrorDiffusionProcessor>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }
    }
}
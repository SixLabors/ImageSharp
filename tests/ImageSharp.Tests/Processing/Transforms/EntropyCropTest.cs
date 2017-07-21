// <copyright file="EntropyCropTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class EntropyCropTest : FileTestBase
    {
        public static readonly TheoryData<float> EntropyCropValues
        = new TheoryData<float>
        {
            .25F,
            .75F
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(EntropyCropValues), DefaultPixelType)]
        public void ImageShouldEntropyCrop<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.EntropyCrop(value)
                    .DebugSave(provider, value);
            }
        }
    }
}
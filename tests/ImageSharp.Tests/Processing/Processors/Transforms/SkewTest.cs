// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Transforms;
    using SixLabors.ImageSharp.Processing.Transforms.Resamplers;

    public class SkewTest : FileTestBase
    {
        public static readonly TheoryData<float, float> SkewValues
            = new TheoryData<float, float>
        {
            { 20, 10 },
            { -20, -10 }
        };

        public static readonly List<string> ResamplerNames
            = new List<string>
        {
            nameof(ResampleMode.Bicubic),
            nameof(ResampleMode.Box),
            nameof(ResampleMode.CatmullRom),
            nameof(ResampleMode.Hermite),
            nameof(ResampleMode.Lanczos2),
            nameof(ResampleMode.Lanczos3),
            nameof(ResampleMode.Lanczos5),
            nameof(ResampleMode.Lanczos8),
            nameof(ResampleMode.MitchellNetravali),
            nameof(ResampleMode.NearestNeighbor),
            nameof(ResampleMode.Robidoux),
            nameof(ResampleMode.RobidouxSharp),
            nameof(ResampleMode.Spline),
            nameof(ResampleMode.Triangle),
            nameof(ResampleMode.Welch),
        };

        [Theory]
        [WithTestPatternImages(nameof(SkewValues), 100, 50, DefaultPixelType)]
        public void ImageShouldSkew<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(i => i.Skew(x, y));
                image.DebugSave(provider, string.Join("_", x, y));
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(SkewValues), 100, 50, DefaultPixelType)]
        public void ImageShouldSkewWithSampler<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
            where TPixel : struct, IPixel<TPixel>
        {
            foreach (string resamplerName in ResamplerNames)
            {
                IResampler sampler = GetResampler(resamplerName);
                using (Image<TPixel> image = provider.GetImage())
                {
                    image.Mutate(i => i.Skew(x, y, sampler));
                    image.DebugSave(provider, string.Join("_", x, y, resamplerName));
                }
            }
        }

        private static IResampler GetResampler(string name)
        {
            PropertyInfo property = typeof(ResampleMode).GetTypeInfo().GetProperty(name);

            if (property == null)
            {
                throw new Exception("Invalid property name!");
            }

            return (IResampler)property.GetValue(null);
        }
    }
}
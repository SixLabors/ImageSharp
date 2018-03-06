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
            nameof(Resamplers.Bicubic),
            nameof(Resamplers.Box),
            nameof(Resamplers.CatmullRom),
            nameof(Resamplers.Hermite),
            nameof(Resamplers.Lanczos2),
            nameof(Resamplers.Lanczos3),
            nameof(Resamplers.Lanczos5),
            nameof(Resamplers.Lanczos8),
            nameof(Resamplers.MitchellNetravali),
            nameof(Resamplers.NearestNeighbor),
            nameof(Resamplers.Robidoux),
            nameof(Resamplers.RobidouxSharp),
            nameof(Resamplers.Spline),
            nameof(Resamplers.Triangle),
            nameof(Resamplers.Welch),
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
            PropertyInfo property = typeof(Resamplers).GetTypeInfo().GetProperty(name);

            if (property == null)
            {
                throw new Exception("Invalid property name!");
            }

            return (IResampler)property.GetValue(null);
        }
    }
}
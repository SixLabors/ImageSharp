// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.ImageSharp.Processing.Transforms.Resamplers;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ProjectiveTransformTests
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.005f, 3);

        public static readonly TheoryData<string> ResamplerNames = new TheoryData<string>
        {
            nameof(KnownResamplers.Bicubic),
            nameof(KnownResamplers.Box),
            nameof(KnownResamplers.CatmullRom),
            nameof(KnownResamplers.Hermite),
            nameof(KnownResamplers.Lanczos2),
            nameof(KnownResamplers.Lanczos3),
            nameof(KnownResamplers.Lanczos5),
            nameof(KnownResamplers.Lanczos8),
            nameof(KnownResamplers.MitchellNetravali),
            nameof(KnownResamplers.NearestNeighbor),
            nameof(KnownResamplers.Robidoux),
            nameof(KnownResamplers.RobidouxSharp),
            nameof(KnownResamplers.Spline),
            nameof(KnownResamplers.Triangle),
            nameof(KnownResamplers.Welch),
        };

        [Theory]
        [WithTestPatternImages(nameof(ResamplerNames), 150, 150, PixelTypes.Rgba32)]
        public void Transform_WithSampler<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
            where TPixel : struct, IPixel<TPixel>
        {
            IResampler sampler = GetResampler(resamplerName);
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix4x4 m = ProjectiveTransformHelper.CreateTaperMatrix(image.Size(), TaperSide.Right, TaperCorner.Both, .5F);

                image.Mutate(i => { i.Transform(m, sampler); });

                image.DebugSave(provider, resamplerName);

                // TODO: Enable and add more tests.
                // image.CompareToReferenceOutput(ValidatorComparer, provider, resamplerName);
            }
        }

        [Theory]
        [WithSolidFilledImages(100, 100, 0, 0, 255, PixelTypes.Rgba32)]
        public void RawTransformMatchesDocumentedExample<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // This test matches the output described in the example at
            // https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/transforms/non-affine
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix4x4 m = Matrix4x4.Identity;
                m.M13 = 0.01F;

                image.Mutate(i => { i.Transform(m); });

                image.DebugSave(provider);

                // TODO: Enable and add more tests.
                // image.CompareToReferenceOutput(ValidatorComparer, provider, resamplerName);
            }
        }

        private static IResampler GetResampler(string name)
        {
            PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

            if (property == null)
            {
                throw new Exception("Invalid property name!");
            }

            return (IResampler)property.GetValue(null);
        }
    }
}

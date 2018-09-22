// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ProjectiveTransformTests
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.03f, 3);
        private static readonly ImageComparer TolerantComparer = ImageComparer.TolerantPercentage(0.5f, 3);

        private ITestOutputHelper Output { get; }
        
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

        public static readonly TheoryData<TaperSide, TaperCorner> TaperMatrixData = new TheoryData<TaperSide, TaperCorner>
        {
            { TaperSide.Bottom, TaperCorner.Both },
            { TaperSide.Bottom, TaperCorner.LeftOrTop },
            { TaperSide.Bottom, TaperCorner.RightOrBottom },

            { TaperSide.Top, TaperCorner.Both },
            { TaperSide.Top, TaperCorner.LeftOrTop },
            { TaperSide.Top, TaperCorner.RightOrBottom },

            { TaperSide.Left, TaperCorner.Both },
            { TaperSide.Left, TaperCorner.LeftOrTop },
            { TaperSide.Left, TaperCorner.RightOrBottom },

            { TaperSide.Right, TaperCorner.Both },
            { TaperSide.Right, TaperCorner.LeftOrTop },
            { TaperSide.Right, TaperCorner.RightOrBottom },

        };

        public ProjectiveTransformTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

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
                image.CompareToReferenceOutput(ValidatorComparer, provider, resamplerName);
            }
        }

        [Theory]
        [WithSolidFilledImages(nameof(TaperMatrixData), 30, 30, nameof(Rgba32.Red), PixelTypes.Rgba32)]
        public void Transform_WithTaperMatrix<TPixel>(TestImageProvider<TPixel> provider, TaperSide taperSide, TaperCorner taperCorner)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix4x4 m = ProjectiveTransformHelper.CreateTaperMatrix(image.Size(), taperSide, taperCorner, .5F);
                image.Mutate(i => { i.Transform(m); });

                FormattableString testOutputDetails = $"{taperSide}-{taperCorner}";
                image.DebugSave(provider, testOutputDetails);
                image.CompareFirstFrameToReferenceOutput(TolerantComparer, provider, testOutputDetails);
            }
        }

        [Theory]
        [WithSolidFilledImages(100, 100, 0, 0, 255, PixelTypes.Rgba32)]
        public void RawTransformMatchesDocumentedExample<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // Printing some extra output to help investigating roundoff errors:
            this.Output.WriteLine($"Vector.IsHardwareAccelerated: {Vector.IsHardwareAccelerated}");

            // This test matches the output described in the example at
            // https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/transforms/non-affine
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix4x4 m = Matrix4x4.Identity;
                m.M13 = 0.01F;

                image.Mutate(i => { i.Transform(m); });

                image.DebugSave(provider);
                image.CompareToReferenceOutput(TolerantComparer, provider);
            }
        }

        private static IResampler GetResampler(string name)
        {
            PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

            if (property is null)
            {
                throw new Exception($"No resampler named {name}");
            }

            return (IResampler)property.GetValue(null);
        }
    }
}
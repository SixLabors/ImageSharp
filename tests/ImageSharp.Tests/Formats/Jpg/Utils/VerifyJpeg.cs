// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    internal static class VerifyJpeg
    {
        internal static void VerifySize(IJpegComponent component, int expectedBlocksX, int expectedBlocksY)
        {
            Assert.Equal(new Size(expectedBlocksX, expectedBlocksY), component.SizeInBlocks);
        }

        internal static void VerifyComponent(
            IJpegComponent component,
            Size expectedSizeInBlocks,
            Size expectedSamplingFactors,
            Size expectedSubsamplingDivisors)
        {
            Assert.Equal(expectedSizeInBlocks, component.SizeInBlocks);
            Assert.Equal(expectedSamplingFactors, component.SamplingFactors);
            Assert.Equal(expectedSubsamplingDivisors, component.SubSamplingDivisors);
        }

        internal static void VerifyComponentSizes3(
            IEnumerable<IJpegComponent> components,
            int xBc0,
            int yBc0,
            int xBc1,
            int yBc1,
            int xBc2,
            int yBc2)
        {
            IJpegComponent[] c = components.ToArray();
            Assert.Equal(3, components.Count());

            VerifySize(c[0], xBc0, yBc0);
            VerifySize(c[1], xBc1, yBc1);
            VerifySize(c[2], xBc2, yBc2);
        }

        internal static void SaveSpectralImage<TPixel>(
            TestImageProvider<TPixel> provider,
            LibJpegTools.SpectralData data,
            ITestOutputHelper output = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            foreach (LibJpegTools.ComponentData comp in data.Components)
            {
                output?.WriteLine("Min: " + comp.MinVal);
                output?.WriteLine("Max: " + comp.MaxVal);

                using (Image<Rgba32> image = comp.CreateGrayScaleImage())
                {
                    string details = $"C{comp.Index}";
                    image.DebugSave(provider, details, appendPixelTypeToFileName: false);
                }
            }

            Image<Rgba32> fullImage = data.TryCreateRGBSpectralImage();

            if (fullImage != null)
            {
                fullImage.DebugSave(provider, "FULL", appendPixelTypeToFileName: false);
                fullImage.Dispose();
            }
        }
    }
}

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    using System.Collections.Generic;
    using System.Linq;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.PixelFormats;

    using Xunit;
    using Xunit.Abstractions;

    internal static class VerifyJpeg
    {
        internal static void ComponentSize(IJpegComponent component, int expectedBlocksX, int expectedBlocksY)
        {
            Assert.Equal(component.WidthInBlocks, expectedBlocksX);
            Assert.Equal(component.HeightInBlocks, expectedBlocksY);
        }

        internal static void Components3(
            IEnumerable<IJpegComponent> components,
            int xBc0, int yBc0,
            int xBc1, int yBc1,
            int xBc2, int yBc2)
        {
            IJpegComponent[] c = components.ToArray();
            Assert.Equal(3, components.Count());

            ComponentSize(c[0], xBc0, yBc0);
            ComponentSize(c[1], xBc1, yBc1);
            ComponentSize(c[2], xBc2, yBc2);
        }

        internal static void SaveSpectralImage<TPixel>(TestImageProvider<TPixel> provider, LibJpegTools.SpectralData data, ITestOutputHelper output = null)
            where TPixel : struct, IPixel<TPixel>
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
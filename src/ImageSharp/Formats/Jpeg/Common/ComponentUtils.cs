using System.Collections.Generic;
using System.Linq;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    using System;

    /// <summary>
    /// Various utilities for <see cref="SubsampleRatio"/> and <see cref="IJpegComponent"/>.
    /// </summary>
    internal static class ComponentUtils
    {
        public static Size SizeInBlocks(this IJpegComponent component) => new Size(component.WidthInBlocks, component.HeightInBlocks);

        public static ref Block8x8 GetBlockReference(this IJpegComponent component, int bx, int by)
        {
            return ref component.SpectralBlocks[bx, by];
        }

        public static SubsampleRatio GetSubsampleRatio(int horizontalRatio, int verticalRatio)
        {
            switch ((horizontalRatio << 4) | verticalRatio)
            {
                case 0x11:
                    return SubsampleRatio.Ratio444;
                case 0x12:
                    return SubsampleRatio.Ratio440;
                case 0x21:
                    return SubsampleRatio.Ratio422;
                case 0x22:
                    return SubsampleRatio.Ratio420;
                case 0x41:
                    return SubsampleRatio.Ratio411;
                case 0x42:
                    return SubsampleRatio.Ratio410;
            }

            return SubsampleRatio.Ratio444;
        }

        public static SubsampleRatio GetSubsampleRatio(IEnumerable<IJpegComponent> components)
        {
            IJpegComponent[] componentArray = components.ToArray();
            if (componentArray.Length == 3)
            {
                int h0 = componentArray[0].HorizontalSamplingFactor;
                int v0 = componentArray[0].VerticalSamplingFactor;
                int horizontalRatio = h0 / componentArray[1].HorizontalSamplingFactor;
                int verticalRatio = v0 / componentArray[1].VerticalSamplingFactor;
                return GetSubsampleRatio(horizontalRatio, verticalRatio);
            }
            else
            {
                return SubsampleRatio.Undefined;
            }
        }

        /// <summary>
        /// Returns the height and width of the chroma components
        /// </summary>
        /// <param name="ratio">The subsampling ratio.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The <see cref="Size"/> of the chrominance channel</returns>
        public static Size CalculateChrominanceSize(this SubsampleRatio ratio, int width, int height)
        {
            switch (ratio)
            {
                case SubsampleRatio.Ratio422:
                    return new Size((width + 1) / 2, height);
                case SubsampleRatio.Ratio420:
                    return new Size((width + 1) / 2, (height + 1) / 2);
                case SubsampleRatio.Ratio440:
                    return new Size(width, (height + 1) / 2);
                case SubsampleRatio.Ratio411:
                    return new Size((width + 3) / 4, height);
                case SubsampleRatio.Ratio410:
                    return new Size((width + 3) / 4, (height + 1) / 2);
                default:
                    // Default to 4:4:4 subsampling.
                    return new Size(width, height);
            }
        }

        public static bool IsChromaComponent(this IJpegComponent component) =>
            component.Index > 0 && component.Index < 3;

        public static Size[] CalculateJpegChannelSizes(IEnumerable<IJpegComponent> components, SubsampleRatio ratio)
        {
            IJpegComponent[] c = components.ToArray();
            Size[] sizes = new Size[c.Length];

            Size s0 = new Size(c[0].WidthInBlocks, c[0].HeightInBlocks) * 8;
            sizes[0] = s0;

            if (c.Length > 1)
            {
                Size chromaSize = ratio.CalculateChrominanceSize(s0.Width, s0.Height);
                sizes[1] = chromaSize;

                if (c.Length > 2)
                {
                    sizes[2] = chromaSize;
                }
            }

            if (c.Length > 3)
            {
                sizes[3] = s0;
            }

            return sizes;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Provides enumeration of the various available subsample ratios.
    /// https://en.wikipedia.org/wiki/Chroma_subsampling
    /// </summary>
    internal enum SubsampleRatio
    {
        Undefined,

        /// <summary>
        /// 4:4:4
        /// </summary>
        Ratio444,

        /// <summary>
        /// 4:2:2
        /// </summary>
        Ratio422,

        /// <summary>
        /// 4:2:0
        /// </summary>
        Ratio420,

        /// <summary>
        /// 4:4:0
        /// </summary>
        Ratio440,

        /// <summary>
        /// 4:1:1
        /// </summary>
        Ratio411,

        /// <summary>
        /// 4:1:0
        /// </summary>
        Ratio410,
    }

    /// <summary>
    /// Various utilities for <see cref="SubsampleRatio"/>
    /// </summary>
    internal static class Subsampling
    {
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
    }
}
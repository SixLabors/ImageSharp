using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    /// <summary>
    /// Various utilities for <see cref="SubsampleRatio"/> and <see cref="IJpegComponent"/>.
    /// </summary>
    internal static class ComponentUtils
    {
        //public static Size SizeInBlocks(this IJpegComponent component) => new Size(component.WidthInBlocks, component.HeightInBlocks);

        // In Jpeg these are really useful operations:

        public static Size MultiplyBy(this Size a, Size b) => new Size(a.Width * b.Width, a.Height * b.Height);

        public static Size DivideBy(this Size a, Size b) => new Size(a.Width / b.Width, a.Height / b.Height);

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

        // https://en.wikipedia.org/wiki/Chroma_subsampling
        public static SubsampleRatio GetSubsampleRatio(IEnumerable<IJpegComponent> components)
        {
            IJpegComponent[] componentArray = components.ToArray();
            if (componentArray.Length == 3)
            {
                Size s0 = componentArray[0].SamplingFactors;
                Size ratio = s0.DivideBy(componentArray[1].SamplingFactors);

                return GetSubsampleRatio(ratio.Width, ratio.Height);
            }
            else
            {
                return SubsampleRatio.Undefined;
            }
        }

        /// <summary>
        /// Returns the height and width of the chroma components
        /// TODO: Not needed by new JpegImagePostprocessor
        /// </summary>
        /// <param name="ratio">The subsampling ratio.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The <see cref="Size"/> of the chrominance channel</returns>
        public static Size CalculateChrominanceSize(this SubsampleRatio ratio, int width, int height)
        {
            (int divX, int divY) = ratio.GetChrominanceSubSampling();
            var size = new Size(width, height);
            return size.GetSubSampledSize(divX, divY);
        }

        // TODO: Find a better place for this method
        public static Size GetSubSampledSize(this Size originalSize, int divX, int divY)
        {
            var sizeVect = (Vector2)(SizeF)originalSize;
            sizeVect /= new Vector2(divX, divY);
            sizeVect.X = MathF.Ceiling(sizeVect.X);
            sizeVect.Y = MathF.Ceiling(sizeVect.Y);

            return new Size((int)sizeVect.X, (int)sizeVect.Y);
        }

        public static Size GetSubSampledSize(this Size originalSize, int subsamplingDivisor) =>
            GetSubSampledSize(originalSize, subsamplingDivisor, subsamplingDivisor);

        // TODO: Not needed by new JpegImagePostprocessor
        public static (int divX, int divY) GetChrominanceSubSampling(this SubsampleRatio ratio)
        {
            switch (ratio)
            {
                case SubsampleRatio.Ratio422: return (2, 1);
                case SubsampleRatio.Ratio420: return (2, 2);
                case SubsampleRatio.Ratio440: return (1, 2);
                case SubsampleRatio.Ratio411: return (4, 1);
                case SubsampleRatio.Ratio410: return (4, 2);
                default: return (1, 1);
            }
        }

        public static bool IsChromaComponent(this IJpegComponent component) =>
            component.Index > 0 && component.Index < 3;

        // TODO: Not needed by new JpegImagePostprocessor
        public static Size[] CalculateJpegChannelSizes(IEnumerable<IJpegComponent> components, SubsampleRatio ratio)
        {
            IJpegComponent[] c = components.ToArray();
            Size[] sizes = new Size[c.Length];

            Size s0 = c[0].SizeInBlocks * 8;
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
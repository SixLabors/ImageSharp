// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The helper methods for the PNG encoder options.
    /// </summary>
    internal static class PngEncoderOptionsHelpers
    {
        /// <summary>
        /// Adjusts the options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pngMetadata">The PNG metadata.</param>
        /// <param name="use16Bit">if set to <c>true</c> [use16 bit].</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        public static void AdjustOptions(
            PngEncoderOptions options,
            PngMetadata pngMetadata,
            out bool use16Bit,
            out int bytesPerPixel)
        {
            // Always take the encoder options over the metadata values.
            options.Gamma = options.Gamma ?? pngMetadata.Gamma;
            options.ColorType = options.ColorType ?? pngMetadata.ColorType;
            options.BitDepth = options.BitDepth ?? pngMetadata.BitDepth;
            options.InterlaceMethod = options.InterlaceMethod ?? pngMetadata.InterlaceMethod;

            use16Bit = options.BitDepth == PngBitDepth.Bit16;
            bytesPerPixel = CalculateBytesPerPixel(options.ColorType, use16Bit);

            // Ensure we are not allowing impossible combinations.
            if (!PngConstants.ColorTypes.ContainsKey(options.ColorType.Value))
            {
                throw new NotSupportedException("Color type is not supported or not valid.");
            }
        }

        /// <summary>
        /// Creates the quantized frame.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="options">The options.</param>
        /// <param name="image">The image.</param>
        public static IQuantizedFrame<TPixel> CreateQuantizedFrame<TPixel>(
            PngEncoderOptions options,
            Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (options.ColorType != PngColorType.Palette)
            {
                return null;
            }

            byte bits = (byte)options.BitDepth;
            if (Array.IndexOf(PngConstants.ColorTypes[options.ColorType.Value], bits) == -1)
            {
                throw new NotSupportedException("Bit depth is not supported or not valid.");
            }

            // Use the metadata to determine what quantization depth to use if no quantizer has been set.
            if (options.Quantizer is null)
            {
                options.Quantizer = new WuQuantizer(ImageMaths.GetColorCountForBitDepth(bits));
            }

            // Create quantized frame returning the palette and set the bit depth.
            using (IFrameQuantizer<TPixel> frameQuantizer = options.Quantizer.CreateFrameQuantizer<TPixel>(image.GetConfiguration()))
            {
                return frameQuantizer.QuantizeFrame(image.Frames.RootFrame);
            }
        }

        /// <summary>
        /// Calculates the bit depth value.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="options">The options.</param>
        /// <param name="image">The image.</param>
        /// <param name="quantizedFrame">The quantized frame.</param>
        public static byte CalculateBitDepth<TPixel>(
            PngEncoderOptions options,
            Image<TPixel> image,
            IQuantizedFrame<TPixel> quantizedFrame)
            where TPixel : struct, IPixel<TPixel>
        {
            byte bitDepth;
            if (options.ColorType == PngColorType.Palette)
            {
                byte quantizedBits = (byte)ImageMaths.GetBitsNeededForColorDepth(quantizedFrame.Palette.Length).Clamp(1, 8);
                byte bits = Math.Max((byte)options.BitDepth, quantizedBits);

                // Png only supports in four pixel depths: 1, 2, 4, and 8 bits when using the PLTE chunk
                // We check again for the bit depth as the bit depth of the color palette from a given quantizer might not
                // be within the acceptable range.
                if (bits == 3)
                {
                    bits = 4;
                }
                else if (bits >= 5 && bits <= 7)
                {
                    bits = 8;
                }

                bitDepth = bits;
            }
            else
            {
                bitDepth = (byte)options.BitDepth;
            }

            if (Array.IndexOf(PngConstants.ColorTypes[options.ColorType.Value], bitDepth) == -1)
            {
                throw new NotSupportedException("Bit depth is not supported or not valid.");
            }

            return bitDepth;
        }

        /// <summary>
        /// Calculates the correct number of bytes per pixel for the given color type.
        /// </summary>
        /// <returns>Bytes per pixel.</returns>
        private static int CalculateBytesPerPixel(PngColorType? pngColorType, bool use16Bit)
        {
            switch (pngColorType)
            {
                case PngColorType.Grayscale:
                    return use16Bit ? 2 : 1;

                case PngColorType.GrayscaleWithAlpha:
                    return use16Bit ? 4 : 2;

                case PngColorType.Palette:
                    return 1;

                case PngColorType.Rgb:
                    return use16Bit ? 6 : 3;

                // PngColorType.RgbWithAlpha
                default:
                    return use16Bit ? 8 : 4;
            }
        }
    }
}

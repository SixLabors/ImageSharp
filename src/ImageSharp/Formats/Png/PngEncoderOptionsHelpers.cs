// Copyright (c) Six Labors.
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
        /// Adjusts the options based upon the given metadata.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pngMetadata">The PNG metadata.</param>
        /// <param name="use16Bit">if set to <c>true</c> [use16 bit].</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        public static void AdjustOptions<TPixel>(
            PngEncoderOptions options,
            PngMetadata pngMetadata,
            out bool use16Bit,
            out int bytesPerPixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Always take the encoder options over the metadata values.
            options.Gamma ??= pngMetadata.Gamma;

            // Use options, then check metadata, if nothing set there then we suggest
            // a sensible default based upon the pixel format.
            options.ColorType ??= pngMetadata.ColorType ?? SuggestColorType<TPixel>();
            options.BitDepth ??= pngMetadata.BitDepth ?? SuggestBitDepth<TPixel>();

            // Ensure bit depth and color type are a supported combination.
            // Bit8 is the only bit depth supported by all color types.
            byte bits = (byte)options.BitDepth;
            byte[] validBitDepths = PngConstants.ColorTypes[options.ColorType.Value];
            if (Array.IndexOf(validBitDepths, bits) == -1)
            {
                options.BitDepth = PngBitDepth.Bit8;
            }

            options.InterlaceMethod ??= pngMetadata.InterlaceMethod;

            use16Bit = options.BitDepth == PngBitDepth.Bit16;
            bytesPerPixel = CalculateBytesPerPixel(options.ColorType, use16Bit);

            if (options.IgnoreMetadata)
            {
                options.ChunkFilter = PngChunkFilter.ExcludeAll;
            }
        }

        /// <summary>
        /// Creates the quantized frame.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="options">The options.</param>
        /// <param name="image">The image.</param>
        public static IndexedImageFrame<TPixel> CreateQuantizedFrame<TPixel>(
            PngEncoderOptions options,
            Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (options.ColorType != PngColorType.Palette)
            {
                return null;
            }

            // Use the metadata to determine what quantization depth to use if no quantizer has been set.
            if (options.Quantizer is null)
            {
                byte bits = (byte)options.BitDepth;
                var maxColors = ColorNumerics.GetColorCountForBitDepth(bits);
                options.Quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = maxColors });
            }

            // Create quantized frame returning the palette and set the bit depth.
            using (IQuantizer<TPixel> frameQuantizer = options.Quantizer.CreatePixelSpecificQuantizer<TPixel>(image.GetConfiguration()))
            {
                ImageFrame<TPixel> frame = image.Frames.RootFrame;
                return frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds());
            }
        }

        /// <summary>
        /// Calculates the bit depth value.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="options">The options.</param>
        /// <param name="quantizedFrame">The quantized frame.</param>
        public static byte CalculateBitDepth<TPixel>(
            PngEncoderOptions options,
            IndexedImageFrame<TPixel> quantizedFrame)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            byte bitDepth;
            if (options.ColorType == PngColorType.Palette)
            {
                byte quantizedBits = (byte)Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(quantizedFrame.Palette.Length), 1, 8);
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
            return pngColorType switch
            {
                PngColorType.Grayscale => use16Bit ? 2 : 1,
                PngColorType.GrayscaleWithAlpha => use16Bit ? 4 : 2,
                PngColorType.Palette => 1,
                PngColorType.Rgb => use16Bit ? 6 : 3,

                // PngColorType.RgbWithAlpha
                _ => use16Bit ? 8 : 4,
            };
        }

        /// <summary>
        /// Returns a suggested <see cref="PngColorType"/> for the given <typeparamref name="TPixel"/>
        /// This is not exhaustive but covers many common pixel formats.
        /// </summary>
        private static PngColorType SuggestColorType<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return typeof(TPixel) switch
            {
                Type t when t == typeof(A8) => PngColorType.GrayscaleWithAlpha,
                Type t when t == typeof(Argb32) => PngColorType.RgbWithAlpha,
                Type t when t == typeof(Bgr24) => PngColorType.Rgb,
                Type t when t == typeof(Bgra32) => PngColorType.RgbWithAlpha,
                Type t when t == typeof(L8) => PngColorType.Grayscale,
                Type t when t == typeof(L16) => PngColorType.Grayscale,
                Type t when t == typeof(La16) => PngColorType.GrayscaleWithAlpha,
                Type t when t == typeof(La32) => PngColorType.GrayscaleWithAlpha,
                Type t when t == typeof(Rgb24) => PngColorType.Rgb,
                Type t when t == typeof(Rgba32) => PngColorType.RgbWithAlpha,
                Type t when t == typeof(Rgb48) => PngColorType.Rgb,
                Type t when t == typeof(Rgba64) => PngColorType.RgbWithAlpha,
                Type t when t == typeof(RgbaVector) => PngColorType.RgbWithAlpha,
                _ => PngColorType.RgbWithAlpha
            };
        }

        /// <summary>
        /// Returns a suggested <see cref="PngBitDepth"/> for the given <typeparamref name="TPixel"/>
        /// This is not exhaustive but covers many common pixel formats.
        /// </summary>
        private static PngBitDepth SuggestBitDepth<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return typeof(TPixel) switch
            {
                Type t when t == typeof(A8) => PngBitDepth.Bit8,
                Type t when t == typeof(Argb32) => PngBitDepth.Bit8,
                Type t when t == typeof(Bgr24) => PngBitDepth.Bit8,
                Type t when t == typeof(Bgra32) => PngBitDepth.Bit8,
                Type t when t == typeof(L8) => PngBitDepth.Bit8,
                Type t when t == typeof(L16) => PngBitDepth.Bit16,
                Type t when t == typeof(La16) => PngBitDepth.Bit8,
                Type t when t == typeof(La32) => PngBitDepth.Bit16,
                Type t when t == typeof(Rgb24) => PngBitDepth.Bit8,
                Type t when t == typeof(Rgba32) => PngBitDepth.Bit8,
                Type t when t == typeof(Rgb48) => PngBitDepth.Bit16,
                Type t when t == typeof(Rgba64) => PngBitDepth.Bit16,
                Type t when t == typeof(RgbaVector) => PngBitDepth.Bit16,
                _ => PngBitDepth.Bit8
            };
        }
    }
}

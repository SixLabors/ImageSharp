// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Contains utility methods for <see cref="IQuantizer{TPixel}"/> instances.
/// </summary>
public static class QuantizerUtilities
{
    /// <summary>
    /// Helper method for throwing an exception when a frame quantizer palette has
    /// been requested but not built yet.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="palette">The frame quantizer palette.</param>
    /// <exception cref="InvalidOperationException">
    /// The palette has not been built via <see cref="IQuantizer{TPixel}.AddPaletteColors"/>
    /// </exception>
    public static void CheckPaletteState<TPixel>(in ReadOnlyMemory<TPixel> palette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (palette.Equals(default))
        {
            throw new InvalidOperationException("Frame Quantizer palette has not been built.");
        }
    }

    /// <summary>
    /// Execute both steps of the quantization.
    /// </summary>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="source">The source image frame to quantize.</param>
    /// <param name="bounds">The bounds within the frame to quantize.</param>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <returns>
    /// A <see cref="IndexedImageFrame{TPixel}"/> representing a quantized version of the source frame pixels.
    /// </returns>
    public static IndexedImageFrame<TPixel> BuildPaletteAndQuantizeFrame<TPixel>(
        this IQuantizer<TPixel> quantizer,
        ImageFrame<TPixel> source,
        Rectangle bounds)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(quantizer, nameof(quantizer));
        Guard.NotNull(source, nameof(source));

        Rectangle interest = Rectangle.Intersect(source.Bounds, bounds);
        Buffer2DRegion<TPixel> region = source.PixelBuffer.GetRegion(interest);

        // Collect the palette. Required before the second pass runs.
        quantizer.AddPaletteColors(region);
        return quantizer.QuantizeFrame(source, bounds);
    }

    /// <summary>
    /// Quantizes an image frame and return the resulting output pixels.
    /// </summary>
    /// <typeparam name="TFrameQuantizer">The type of frame quantizer.</typeparam>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="source">The source image frame to quantize.</param>
    /// <param name="bounds">The bounds within the frame to quantize.</param>
    /// <returns>
    /// A <see cref="IndexedImageFrame{TPixel}"/> representing a quantized version of the source frame pixels.
    /// </returns>
    public static IndexedImageFrame<TPixel> QuantizeFrame<TFrameQuantizer, TPixel>(
        ref TFrameQuantizer quantizer,
        ImageFrame<TPixel> source,
        Rectangle bounds)
        where TFrameQuantizer : struct, IQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(source, nameof(source));
        Rectangle interest = Rectangle.Intersect(source.Bounds, bounds);

        IndexedImageFrame<TPixel> destination = new(
            quantizer.Configuration,
            interest.Width,
            interest.Height,
            quantizer.Palette);

        if (quantizer.Options.Dither is null)
        {
            SecondPass(ref quantizer, source, destination, interest);
        }
        else
        {
            // We clone the image as we don't want to alter the original via error diffusion based dithering.
            using ImageFrame<TPixel> clone = source.Clone();
            SecondPass(ref quantizer, clone, destination, interest);
        }

        return destination;
    }

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel regions.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="pixelSamplingStrategy">The pixel sampling strategy.</param>
    /// <param name="source">The source image to sample from.</param>
    public static void BuildPalette<TPixel>(
        this IQuantizer<TPixel> quantizer,
        IPixelSamplingStrategy pixelSamplingStrategy,
        Image<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
        => quantizer.BuildPalette(source.Configuration, TransparentColorMode.Preserve, pixelSamplingStrategy, source);

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel regions.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="mode">The transparent color mode.</param>
    /// <param name="pixelSamplingStrategy">The pixel sampling strategy.</param>
    /// <param name="source">The source image to sample from.</param>
    public static void BuildPalette<TPixel>(
        this IQuantizer<TPixel> quantizer,
        Configuration configuration,
        TransparentColorMode mode,
        IPixelSamplingStrategy pixelSamplingStrategy,
        Image<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (EncodingUtilities.ShouldClearTransparentPixels<TPixel>(mode))
        {
            foreach (Buffer2DRegion<TPixel> region in pixelSamplingStrategy.EnumeratePixelRegions(source))
            {
                using Buffer2D<TPixel> clone = region.Buffer.CloneRegion(configuration, region.Rectangle);
                quantizer.AddPaletteColors(clone.GetRegion());
            }
        }
        else
        {
            foreach (Buffer2DRegion<TPixel> region in pixelSamplingStrategy.EnumeratePixelRegions(source))
            {
                quantizer.AddPaletteColors(region);
            }
        }
    }

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel regions.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="pixelSamplingStrategy">The pixel sampling strategy.</param>
    /// <param name="source">The source image frame to sample from.</param>
    public static void BuildPalette<TPixel>(
        this IQuantizer<TPixel> quantizer,
        IPixelSamplingStrategy pixelSamplingStrategy,
        ImageFrame<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
        => quantizer.BuildPalette(source.Configuration, TransparentColorMode.Preserve, pixelSamplingStrategy, source);

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel regions.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="mode">The transparent color mode.</param>
    /// <param name="pixelSamplingStrategy">The pixel sampling strategy.</param>
    /// <param name="source">The source image frame to sample from.</param>
    public static void BuildPalette<TPixel>(
        this IQuantizer<TPixel> quantizer,
        Configuration configuration,
        TransparentColorMode mode,
        IPixelSamplingStrategy pixelSamplingStrategy,
        ImageFrame<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (EncodingUtilities.ShouldClearTransparentPixels<TPixel>(mode))
        {
            foreach (Buffer2DRegion<TPixel> region in pixelSamplingStrategy.EnumeratePixelRegions(source))
            {
                using Buffer2D<TPixel> clone = region.Buffer.CloneRegion(configuration, region.Rectangle);
                quantizer.AddPaletteColors(clone.GetRegion());
            }
        }
        else
        {
            foreach (Buffer2DRegion<TPixel> region in pixelSamplingStrategy.EnumeratePixelRegions(source))
            {
                quantizer.AddPaletteColors(region);
            }
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void SecondPass<TFrameQuantizer, TPixel>(
        ref TFrameQuantizer quantizer,
        ImageFrame<TPixel> source,
        IndexedImageFrame<TPixel> destination,
        Rectangle bounds)
        where TFrameQuantizer : struct, IQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IDither? dither = quantizer.Options.Dither;
        Buffer2D<TPixel> sourceBuffer = source.PixelBuffer;

        if (dither is null)
        {
            int offsetY = bounds.Top;
            int offsetX = bounds.Left;

            for (int y = 0; y < destination.Height; y++)
            {
                ReadOnlySpan<TPixel> sourceRow = sourceBuffer.DangerousGetRowSpan(y + offsetY);
                Span<byte> destinationRow = destination.GetWritablePixelRowSpanUnsafe(y);

                for (int x = 0; x < destinationRow.Length; x++)
                {
                    destinationRow[x] = Unsafe.AsRef(in quantizer).GetQuantizedColor(sourceRow[x + offsetX], out TPixel _);
                }
            }

            return;
        }

        dither.ApplyQuantizationDither(ref quantizer, source, destination, bounds);
    }
}

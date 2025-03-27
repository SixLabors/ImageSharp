// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
    internal static QuantizerOptions DeepClone(this QuantizerOptions options, Action<QuantizerOptions>? mutate)
    {
        QuantizerOptions clone = options.DeepClone();
        mutate?.Invoke(clone);
        return clone;
    }

    /// <summary>
    /// Determines if transparent pixels can be replaced based on the specified color mode and pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="threshold">The alpha threshold used to determine if a pixel is transparent.</param>
    /// <returns>Returns true if transparent pixels can be replaced; otherwise, false.</returns>
    public static bool ShouldReplacePixelsByAlphaThreshold<TPixel>(float threshold)
        where TPixel : unmanaged, IPixel<TPixel>
        => threshold > 0 && TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated;

    /// <summary>
    /// Replaces pixels in a span with fully transparent pixels based on an alpha threshold.
    /// </summary>
    /// <param name="source">A span of color vectors that will be checked for transparency and potentially modified.</param>
    /// <param name="threshold">The alpha threshold used to determine if a pixel is transparent.</param>
    public static void ReplacePixelsByAlphaThreshold(Span<Vector4> source, float threshold)
    {
        if (Vector512.IsHardwareAccelerated && source.Length >= 4)
        {
            Vector512<float> threshold512 = Vector512.Create(threshold);
            Span<Vector512<float>> source512 = MemoryMarshal.Cast<Vector4, Vector512<float>>(source);
            for (int i = 0; i < source512.Length; i++)
            {
                ref Vector512<float> v = ref source512[i];

                // Do `vector < threshold`
                Vector512<float> mask = Vector512.LessThan(v, threshold512);

                // Replicate the result for W to all elements (is AllBitsSet if the W was less than threshold and Zero otherwise)
                mask = Vector512.Shuffle(mask, Vector512.Create(3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v512 & ~mask)
                v = Vector512.ConditionalSelect(mask, Vector512<float>.Zero, v);
            }

            int m = Numerics.Modulo4(source.Length);
            if (m != 0)
            {
                for (int i = source.Length - m; i < source.Length; i++)
                {
                    if (source[i].W < threshold)
                    {
                        source[i] = Vector4.Zero;
                    }
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && source.Length >= 2)
        {
            Vector256<float> threshold256 = Vector256.Create(threshold);
            Span<Vector256<float>> source256 = MemoryMarshal.Cast<Vector4, Vector256<float>>(source);
            for (int i = 0; i < source256.Length; i++)
            {
                ref Vector256<float> v = ref source256[i];

                // Do `vector < threshold`
                Vector256<float> mask = Vector256.LessThan(v, threshold256);

                // Replicate the result for W to all elements (is AllBitsSet if the W was less than threshold and Zero otherwise)
                mask = Vector256.Shuffle(mask, Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v256 & ~mask)
                v = Vector256.ConditionalSelect(mask, Vector256<float>.Zero, v);
            }

            int m = Numerics.Modulo2(source.Length);
            if (m != 0)
            {
                for (int i = source.Length - m; i < source.Length; i++)
                {
                    if (source[i].W < threshold)
                    {
                        source[i] = Vector4.Zero;
                    }
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> threshold128 = Vector128.Create(threshold);

            for (int i = 0; i < source.Length; i++)
            {
                ref Vector4 v = ref source[i];
                Vector128<float> v128 = v.AsVector128();

                // Do `vector < threshold`
                Vector128<float> mask = Vector128.LessThan(v128, threshold128);

                // Replicate the result for W to all elements (is AllBitsSet if the W was less than threshold and Zero otherwise)
                mask = Vector128.Shuffle(mask, Vector128.Create(3, 3, 3, 3));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v128 & ~mask)
                v = Vector128.ConditionalSelect(mask, Vector128<float>.Zero, v128).AsVector4();
            }
        }
        else
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].W < threshold)
                {
                    source[i] = Vector4.Zero;
                }
            }
        }
    }

    /// <summary>
    /// Helper method for throwing an exception when a frame quantizer palette has
    /// been requested but not built yet.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="palette">The frame quantizer palette.</param>
    /// <exception cref="InvalidOperationException">
    /// The palette has not been built via <see cref="IQuantizer{TPixel}.AddPaletteColors(in Buffer2DRegion{TPixel})"/>
    /// </exception>
    [MethodImpl(InliningOptions.ColdPath)]
    public static void CheckPaletteState<TPixel>(in ReadOnlyMemory<TPixel> palette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (palette.IsEmpty)
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
        => BuildPaletteAndQuantizeFrame(
            quantizer,
            source,
            bounds,
            TransparentColorMode.Preserve);

    /// <summary>
    /// Execute both steps of the quantization.
    /// </summary>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="source">The source image frame to quantize.</param>
    /// <param name="bounds">The bounds within the frame to quantize.</param>
    /// <param name="mode">The transparent color mode.</param>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <returns>
    /// A <see cref="IndexedImageFrame{TPixel}"/> representing a quantized version of the source frame pixels.
    /// </returns>
    public static IndexedImageFrame<TPixel> BuildPaletteAndQuantizeFrame<TPixel>(
        this IQuantizer<TPixel> quantizer,
        ImageFrame<TPixel> source,
        Rectangle bounds,
        TransparentColorMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(quantizer, nameof(quantizer));
        Guard.NotNull(source, nameof(source));

        Rectangle interest = Rectangle.Intersect(source.Bounds, bounds);
        Buffer2DRegion<TPixel> region = source.PixelBuffer.GetRegion(interest);

        quantizer.AddPaletteColors(in region, mode);
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
    /// <param name="mode">The transparent color mode.</param>
    /// <returns>
    /// A <see cref="IndexedImageFrame{TPixel}"/> representing a quantized version of the source frame pixels.
    /// </returns>
    public static IndexedImageFrame<TPixel> QuantizeFrame<TFrameQuantizer, TPixel>(
        ref TFrameQuantizer quantizer,
        ImageFrame<TPixel> source,
        Rectangle bounds,
        TransparentColorMode mode)
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
            SecondPass(ref quantizer, source, destination, interest, mode);
        }
        else
        {
            // We clone the image as we don't want to alter the original via error diffusion based dithering.
            using ImageFrame<TPixel> clone = source.Clone();
            SecondPass(ref quantizer, clone, destination, interest, mode);
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
        => quantizer.BuildPalette(
            TransparentColorMode.Preserve,
            pixelSamplingStrategy,
            source);

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel regions.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="mode">The transparent color mode.</param>
    /// <param name="pixelSamplingStrategy">The pixel sampling strategy.</param>
    /// <param name="source">The source image to sample from.</param>
    public static void BuildPalette<TPixel>(
        this IQuantizer<TPixel> quantizer,
        TransparentColorMode mode,
        IPixelSamplingStrategy pixelSamplingStrategy,
        Image<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (Buffer2DRegion<TPixel> region in pixelSamplingStrategy.EnumeratePixelRegions(source))
        {
            quantizer.AddPaletteColors(in region, mode);
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
        => quantizer.BuildPalette(
            TransparentColorMode.Preserve,
            pixelSamplingStrategy,
            source);

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel regions.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="quantizer">The pixel specific quantizer.</param>
    /// <param name="mode">The transparent color mode.</param>
    /// <param name="pixelSamplingStrategy">The pixel sampling strategy.</param>
    /// <param name="source">The source image frame to sample from.</param>
    public static void BuildPalette<TPixel>(
        this IQuantizer<TPixel> quantizer,
        TransparentColorMode mode,
        IPixelSamplingStrategy pixelSamplingStrategy,
        ImageFrame<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (Buffer2DRegion<TPixel> region in pixelSamplingStrategy.EnumeratePixelRegions(source))
        {
            quantizer.AddPaletteColors(in region, mode);
        }
    }

    internal static void AddPaletteColors<TFrameQuantizer, TPixel, TPixel2, TDelegate>(
        ref TFrameQuantizer quantizer,
        in Buffer2DRegion<TPixel> source,
        in TDelegate rowDelegate)
        where TFrameQuantizer : struct, IQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
        where TPixel2 : unmanaged, IPixel<TPixel2>
        where TDelegate : struct, IQuantizingPixelRowDelegate<TPixel2>
    {
        Configuration configuration = quantizer.Configuration;
        float threshold = quantizer.Options.TransparencyThreshold;
        TransparentColorMode mode = quantizer.Options.TransparentColorMode;

        using IMemoryOwner<TPixel2> delegateRowOwner = configuration.MemoryAllocator.Allocate<TPixel2>(source.Width);
        Span<TPixel2> delegateRow = delegateRowOwner.Memory.Span;

        bool replaceByThreshold = ShouldReplacePixelsByAlphaThreshold<TPixel>(threshold);
        bool replaceTransparent = EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(rowDelegate.TransparentColorMode);

        if (replaceByThreshold || replaceTransparent)
        {
            using IMemoryOwner<Vector4> vectorRowOwner = configuration.MemoryAllocator.Allocate<Vector4>(source.Width);
            Span<Vector4> vectorRow = vectorRowOwner.Memory.Span;

            if (replaceByThreshold)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    Span<TPixel> sourceRow = source.DangerousGetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(configuration, sourceRow, vectorRow, PixelConversionModifiers.Scale);

                    ReplacePixelsByAlphaThreshold(vectorRow, threshold);

                    PixelOperations<TPixel2>.Instance.FromVector4Destructive(configuration, vectorRow, delegateRow, PixelConversionModifiers.Scale);
                    rowDelegate.Invoke(delegateRow, y);
                }
            }
            else
            {
                for (int y = 0; y < source.Height; y++)
                {
                    Span<TPixel> sourceRow = source.DangerousGetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(configuration, sourceRow, vectorRow, PixelConversionModifiers.Scale);

                    EncodingUtilities.ReplaceTransparentPixels(vectorRow);

                    PixelOperations<TPixel2>.Instance.FromVector4Destructive(configuration, vectorRow, delegateRow, PixelConversionModifiers.Scale);
                    rowDelegate.Invoke(delegateRow, y);
                }
            }
        }
        else
        {
            for (int y = 0; y < source.Height; y++)
            {
                Span<TPixel> sourceRow = source.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.To(configuration, sourceRow, delegateRow);
                rowDelegate.Invoke(delegateRow, y);
            }
        }
    }

    private static void SecondPass<TFrameQuantizer, TPixel>(
        ref TFrameQuantizer quantizer,
        ImageFrame<TPixel> source,
        IndexedImageFrame<TPixel> destination,
        Rectangle bounds,
        TransparentColorMode mode)
        where TFrameQuantizer : struct, IQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        float threshold = quantizer.Options.TransparencyThreshold;
        bool replaceByThreshold = ShouldReplacePixelsByAlphaThreshold<TPixel>(threshold);
        bool replaceTransparent = EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(mode);

        IDither? dither = quantizer.Options.Dither;
        Buffer2D<TPixel> sourceBuffer = source.PixelBuffer;
        Buffer2DRegion<TPixel> region = sourceBuffer.GetRegion(bounds);

        Configuration configuration = quantizer.Configuration;
        using IMemoryOwner<Vector4> vectorOwner = configuration.MemoryAllocator.Allocate<Vector4>(region.Width);
        Span<Vector4> vectorRow = vectorOwner.Memory.Span;

        if (dither is null)
        {
            using IMemoryOwner<TPixel> quantizingRowOwner = configuration.MemoryAllocator.Allocate<TPixel>(region.Width);
            Span<TPixel> quantizingRow = quantizingRowOwner.Memory.Span;

            // This is NOT a clone so we DO NOT write back to the source.
            if (replaceByThreshold || replaceTransparent)
            {
                if (replaceByThreshold)
                {
                    for (int y = 0; y < region.Height; y++)
                    {
                        Span<TPixel> sourceRow = region.DangerousGetRowSpan(y);
                        PixelOperations<TPixel>.Instance.ToVector4(configuration, sourceRow, vectorRow, PixelConversionModifiers.Scale);

                        ReplacePixelsByAlphaThreshold(vectorRow, threshold);

                        PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorRow, quantizingRow, PixelConversionModifiers.Scale);

                        Span<byte> destinationRow = destination.GetWritablePixelRowSpanUnsafe(y);
                        for (int x = 0; x < destinationRow.Length; x++)
                        {
                            destinationRow[x] = quantizer.GetQuantizedColor(quantizingRow[x], out TPixel _);
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < region.Height; y++)
                    {
                        Span<TPixel> sourceRow = region.DangerousGetRowSpan(y);
                        PixelOperations<TPixel>.Instance.ToVector4(configuration, sourceRow, vectorRow, PixelConversionModifiers.Scale);

                        EncodingUtilities.ReplaceTransparentPixels(vectorRow);

                        PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorRow, quantizingRow, PixelConversionModifiers.Scale);

                        Span<byte> destinationRow = destination.GetWritablePixelRowSpanUnsafe(y);
                        for (int x = 0; x < destinationRow.Length; x++)
                        {
                            destinationRow[x] = quantizer.GetQuantizedColor(quantizingRow[x], out TPixel _);
                        }
                    }
                }

                return;
            }

            for (int y = 0; y < region.Height; y++)
            {
                ReadOnlySpan<TPixel> sourceRow = region.DangerousGetRowSpan(y);
                Span<byte> destinationRow = destination.GetWritablePixelRowSpanUnsafe(y);

                for (int x = 0; x < destinationRow.Length; x++)
                {
                    destinationRow[x] = quantizer.GetQuantizedColor(sourceRow[x], out TPixel _);
                }
            }

            return;
        }

        // This is a clone so we write back to the source.
        if (replaceByThreshold || replaceTransparent)
        {
            if (replaceByThreshold)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Span<TPixel> sourceRow = region.DangerousGetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(configuration, sourceRow, vectorRow, PixelConversionModifiers.Scale);

                    ReplacePixelsByAlphaThreshold(vectorRow, threshold);

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorRow, sourceRow, PixelConversionModifiers.Scale);
                }
            }
            else
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Span<TPixel> sourceRow = region.DangerousGetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(configuration, sourceRow, vectorRow, PixelConversionModifiers.Scale);

                    EncodingUtilities.ReplaceTransparentPixels(vectorRow);

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorRow, sourceRow, PixelConversionModifiers.Scale);
                }
            }
        }

        dither.ApplyQuantizationDither(ref quantizer, source, destination, bounds);
    }
}

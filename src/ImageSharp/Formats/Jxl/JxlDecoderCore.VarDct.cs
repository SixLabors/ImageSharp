// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

internal sealed partial class JxlDecoderCore
{
    /// <summary>
    /// Expands color and optional integer alpha with one shared component workspace after all modular transforms complete.
    /// </summary>
    /// <param name="header">The frame's upsampling weights and extra-channel precision.</param>
    /// <param name="frame">The final output size and effective color and alpha factors.</param>
    /// <param name="varDct">The owner of restored float color planes.</param>
    /// <param name="modular">The owner of completed integer extra channels.</param>
    /// <param name="alphaChannel">The alpha extra-channel index, or minus one for no alpha.</param>
    /// <param name="cancellationToken">The token checked during sample conversion and expansion.</param>
    /// <returns>The owned final float alpha plane, or null when alpha needs no upsampling. The caller disposes a returned plane.</returns>
    private Buffer2D<float>? UpsampleVarDctFrame(
        JxlHeader header, JxlFrameHeader frame, JxlVarDctDecoder varDct, JxlModularDecoder modular, int alphaChannel, CancellationToken cancellationToken)
    {
        int colorFactor = frame.Upsampling;
        int alphaFactor = alphaChannel < 0 ? 1 : frame.ExtraChannelUpsampling[alphaChannel];
        int maximumFactor = Math.Max(colorFactor, alphaFactor);
        if (maximumFactor == 1)
        {
            return null;
        }

        // The parser requires extra-channel factors to be at least the color factor. A coded alpha plane
        // therefore fits the color row window. When color stays at full size, reserve only the smaller alpha window.
        Size maximumSourceSize = colorFactor > 1 ? frame.CodingSize : modular.GetChannel(alphaChannel).Size;
        Configuration configuration = this.Options.Configuration;
        using JxlUpsampler upsampler = new(header, frame.OutputSize, maximumSourceSize, maximumFactor, configuration.MemoryAllocator);
        if (colorFactor > 1)
        {
            varDct.ApplyUpsampling(upsampler, colorFactor, cancellationToken);
        }

        if (alphaFactor == 1)
        {
            return null;
        }

        // One final-capacity float plane retains alpha until pixel packing. Its source integers remain
        // owned by the modular decoder. Color and alpha reuse the same kernels and row window above.
        Buffer2D<float> alpha = configuration.MemoryAllocator.Allocate2D<float>(frame.OutputSize);
        try
        {
            Buffer2DRegion<int> source = modular.GetChannel(alphaChannel);
            int precision = header.Metadata.ExtraChannels[alphaChannel].BitDepth.BitsPerSample;
            float maximum = MathF.ScaleB(1F, precision) - 1F;
            for (int y = 0; y < source.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Span<float> target = alpha.DangerousGetRowSpan(y)[..source.Width];
                TensorPrimitives.ConvertChecked<int, float>(source.DangerousGetRowSpan(y), target);
                TensorPrimitives.Divide(target, maximum, target);
            }

            // Lossy reconstruction can produce alpha below zero or above one. Preserve those samples
            // through interpolation; only the destination pixel API applies the normalized alpha bounds.
            upsampler.Apply(alpha, source.Size, alphaFactor, cancellationToken);
            return alpha;
        }
        catch
        {
            alpha.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Converts restored VarDCT planes to working sRGB and packs the requested pixels once.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="header">The image's original color description and XYB transform.</param>
    /// <param name="varDct">The completed color reconstruction, including chroma upsampling and restoration.</param>
    /// <param name="modular">The completed native extra channels.</param>
    /// <param name="destination">The final frame.</param>
    /// <param name="upsampledAlpha">The borrowed expanded alpha plane, or null for native-resolution alpha.</param>
    /// <param name="alphaChannel">The modular alpha index, or minus one for opaque input.</param>
    /// <param name="alphaDepth">The integer alpha precision.</param>
    /// <param name="alphaRepresentation">The encoded RGB association.</param>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    private void PackVarDctFrame<TPixel>(
        JxlHeader header,
        JxlVarDctDecoder varDct,
        JxlModularDecoder modular,
        ImageFrame<TPixel> destination,
        Buffer2DRegion<float>? upsampledAlpha,
        int alphaChannel,
        JxlBitDepth alphaDepth,
        PixelAlphaRepresentation alphaRepresentation,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = this.Options.Configuration;
        int width = destination.Width;
        Buffer2DRegion<float> component0 = varDct.GetReconstructedChannel(0);
        Buffer2DRegion<float> component1 = varDct.GetReconstructedChannel(1);
        Buffer2DRegion<float> component2 = varDct.GetReconstructedChannel(2);
        using IMemoryOwner<Vector4> vectorOwner = configuration.MemoryAllocator.Allocate<Vector4>(width);
        using IMemoryOwner<Rgb>? rgbOwner = header.UsesXybTransform ? configuration.MemoryAllocator.Allocate<Rgb>(width) : null;
        using IMemoryOwner<float>? alphaOwner = alphaChannel < 0 || upsampledAlpha.HasValue
            ? null
            : configuration.MemoryAllocator.Allocate<float>(width);

        Span<Vector4> vectors = vectorOwner.Memory.Span;
        Span<Rgb> rgb = rgbOwner is null ? [] : rgbOwner.Memory.Span;
        Span<float> alpha = alphaOwner is null ? [] : alphaOwner.Memory.Span;
        float alphaMaximum = MathF.ScaleB(1F, alphaDepth.BitsPerSample) - 1F;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;
        ColorConversionOptions conversion = new()
        {
            MemoryAllocator = configuration.MemoryAllocator,
            SourceRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb,
            XybTransform = header.XybTransform
        };

        // Packed XYB needs three floats per sample and fits in the four-float vector row. The RGB row
        // remains separate: conversion finishes reading XYB before pixel vectors overwrite that storage.
        // Both model types use sequential three-float layouts. Alpha stays in its own row until conversion finishes.
        Span<Xyb> xyb = header.UsesXybTransform ? MemoryMarshal.Cast<Vector4, Xyb>(vectors)[..width] : [];
        bool convertGamma = !header.UsesXybTransform && header.Gamma.HasValue;
        double expansionGamma = header.Gamma is Rational gamma ? (double)gamma.Denominator / gamma.Numerator : 1D;

        for (int y = 0; y < destination.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<float> rowAlpha = alpha;
            if (upsampledAlpha.HasValue)
            {
                rowAlpha = upsampledAlpha.Value.DangerousGetRowSpan(y);
            }
            else if (alphaChannel >= 0)
            {
                // Extra channels remain integers through all modular transforms. Normalize their stored
                // precision only at output; this row is reused, and no float alpha image is allocated.
                TensorPrimitives.ConvertChecked<int, float>(modular.GetChannel(alphaChannel).DangerousGetRowSpan(y), alpha);
                TensorPrimitives.Divide(alpha, alphaMaximum, alpha);
            }

            if (header.UsesXybTransform)
            {
                // XYB reconstructs working sRGB directly from its encoded matrix and intensity target.
                // An embedded profile describes the original image's suggested output space; it must
                // not be applied to these reconstructed sRGB samples as though they were device RGB.
                // A unit scale preserves finite values, including signed zero. The shared interleave
                // and model methods own SIMD widths and scalar tails; each model call receives the complete row.
                SimdUtils.PackedNormalizeInterleave3(
                    component0.DangerousGetRowSpan(y),
                    component1.DangerousGetRowSpan(y),
                    component2.DangerousGetRowSpan(y),
                    MemoryMarshal.Cast<Xyb, float>(xyb),
                    1F);

                Xyb.ToProfileConnectionSpace(conversion, xyb, rgb);
                Rgb.ToScaledVector4(rgb, vectors);
                if (!rowAlpha.IsEmpty)
                {
                    // RGB supplies opaque W values. Restore encoded alpha only when it exists;
                    // the destination pixel conversion below still owns its bounds and association.
                    for (int x = 0; x < width; x++)
                    {
                        vectors[x].W = rowAlpha[x];
                    }
                }
            }
            else
            {
                SimdUtils.InterleaveFloatPlanes(
                    component0.DangerousGetRowSpan(y), component1.DangerousGetRowSpan(y), component2.DangerousGetRowSpan(y), rowAlpha, vectors);

                if (convertGamma)
                {
                    if (alphaRepresentation == PixelAlphaRepresentation.Associated)
                    {
                        PixelOperations.UnPremultiply(vectors, vectors);
                    }

                    GammaCompanding.Expand(vectors, expansionGamma);
                    SRgbCompanding.Compress(vectors);
                }
            }

            // Color and filters retain float samples. The destination pixel API owns its finite range,
            // alpha conversion and final rounding. Associated alpha is removed after the color transform,
            // matching the format's output order; gamma conversion has already removed it above.
            Span<TPixel> pixels = destination.PixelBuffer.DangerousGetRowSpan(y);
            if (!convertGamma && alphaRepresentation == PixelAlphaRepresentation.Associated)
            {
                operations.FromAssociatedScaledVector4Destructive(configuration, vectors, pixels);
            }
            else
            {
                operations.FromUnassociatedScaledVector4Destructive(configuration, vectors, pixels);
            }
        }
    }
}

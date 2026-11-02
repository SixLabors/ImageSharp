// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
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
    /// Converts restored VarDCT planes to working sRGB and packs the requested pixels once.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="header">The image's original color description and XYB transform.</param>
    /// <param name="varDct">The completed color reconstruction, including chroma upsampling and restoration.</param>
    /// <param name="modular">The completed native extra channels.</param>
    /// <param name="destination">The final frame.</param>
    /// <param name="alphaChannel">The modular alpha index, or minus one for opaque input.</param>
    /// <param name="alphaDepth">The integer alpha precision.</param>
    /// <param name="alphaRepresentation">The encoded RGB association.</param>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    private void PackVarDctFrame<TPixel>(
        JxlHeader header,
        JxlVarDctDecoder varDct,
        JxlModularDecoder modular,
        ImageFrame<TPixel> destination,
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
        using IMemoryOwner<float>? alphaOwner = alphaChannel < 0 ? null : configuration.MemoryAllocator.Allocate<float>(width);
        Span<Vector4> vectors = vectorOwner.Memory.Span;
        Span<float> alpha = alphaOwner is null ? [] : alphaOwner.Memory.Span;
        float alphaMaximum = MathF.ScaleB(1F, alphaDepth.BitsPerSample) - 1F;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;
        ColorConversionOptions conversion = new()
        {
            MemoryAllocator = configuration.MemoryAllocator,
            SourceRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb,
            XybTransform = header.XybTransform
        };

        // One stack block holds a full sixteen-color Vector512 group. The shared model APIs own all
        // SIMD arithmetic and scalar tails. Their three-component storage does not contain alpha.
        InlineArray16<Xyb> xybStorage = default;
        InlineArray16<Rgb> rgbStorage = default;
        InlineArray16<Vector4> convertedStorage = default;
        Span<Xyb> xyb = xybStorage;
        Span<Rgb> rgb = rgbStorage;
        Span<Vector4> converted = convertedStorage;
        bool convertGamma = !header.UsesXybTransform && header.Gamma.HasValue;
        double expansionGamma = header.Gamma is Rational gamma ? (double)gamma.Denominator / gamma.Numerator : 1D;

        for (int y = 0; y < destination.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (alphaChannel >= 0)
            {
                // Extra channels remain integers through all modular transforms. Normalize their stored
                // precision only at output; this row is reused, and no float alpha image is allocated.
                TensorPrimitives.ConvertChecked<int, float>(modular.GetChannel(alphaChannel).DangerousGetRowSpan(y), alpha);
                TensorPrimitives.Divide(alpha, alphaMaximum, alpha);
            }

            SimdUtils.InterleaveFloatPlanes(
                component0.DangerousGetRowSpan(y), component1.DangerousGetRowSpan(y), component2.DangerousGetRowSpan(y), alpha, vectors);

            if (header.UsesXybTransform)
            {
                // XYB reconstructs working sRGB directly from its encoded matrix and intensity target.
                // An embedded profile describes the original image's suggested output space; it must
                // not be applied to these reconstructed sRGB samples as though they were device RGB.
                for (int x = 0; x < width; x += xyb.Length)
                {
                    int count = Math.Min(xyb.Length, width - x);
                    Span<Vector4> block = vectors.Slice(x, count);
                    Xyb.FromScaledVector4(block, xyb[..count]);
                    Xyb.ToProfileConnectionSpace(conversion, xyb[..count], rgb[..count]);
                    Rgb.ToScaledVector4(rgb[..count], converted[..count]);
                    for (int i = 0; i < count; i++)
                    {
                        block[i] = Numerics.WithW(converted[i], block[i]);
                    }
                }
            }
            else if (convertGamma)
            {
                if (alphaRepresentation == PixelAlphaRepresentation.Associated)
                {
                    PixelOperations.UnPremultiply(vectors, vectors);
                }

                GammaCompanding.Expand(vectors, expansionGamma);
                SRgbCompanding.Compress(vectors);
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

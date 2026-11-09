// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// Represents an RGB working space with the hybrid log-gamma transfer curve and display-luminance adjustment.
/// </summary>
/// <remarks>
/// HLG names the hybrid log-gamma system. ITU-R BT.2100 defines its display-luminance adjustment.
/// See <see href="https://www.itu.int/dms_pubrec/itu-r/rec/bt/R-REC-BT.2100-2-201807-S!!PDF-E.pdf">ITU-R BT.2100-2</see>, Table 5 and Note 5f.
/// A linear component of one represents <see cref="PeakLuminance"/> cd/m².
/// The component curve extends negative values with their original sign. Display-luminance adjustment requires nonnegative physical luminance.
/// Expansion accepts nonlinear HLG RGB and produces display-linear RGB relative to the selected display peak.
/// Compression accepts that display-linear RGB and produces nonlinear HLG RGB.
/// Luminance adjustment is identity when its gamma differs from one by at most 0.01. Its RGB scale is limited to 1,000,000,000.
/// A nonfinite luminance scale leaves the RGB components unchanged.
/// Compression applies the same limits to the inverse gamma. These limits bound luminance adjustment, not the component-curve input.
/// Converted RGB is bounded to the normalized range [-65504,65504]. Alpha remains unchanged.
/// </remarks>
public sealed partial class HlgWorkingSpace : RgbWorkingSpace
{
    private readonly ExpandOperator expand;
    private readonly CompressOperator compress;

    /// <summary>
    /// Initializes a new instance of the <see cref="HlgWorkingSpace"/> class.
    /// </summary>
    /// <param name="peakLuminance">The positive finite display peak luminance, in cd/m².</param>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity coordinates of the RGB primaries.</param>
    public HlgWorkingSpace(float peakLuminance, CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : base(referenceWhite, chromaticityCoordinates)
    {
        Guard.MustBeGreaterThan(peakLuminance, 0F, nameof(peakLuminance));
        Guard.MustBeLessThanOrEqualTo(peakLuminance, float.MaxValue, nameof(peakLuminance));

        this.PeakLuminance = peakLuminance;

        // BT.2100's extended luminance model also covers peaks outside the usual 400–2000 cd/m² interval.
        // Prepare both directions in their defined order so compression does not add a rounded reciprocal of the expansion gamma.
        float relativePeak = peakLuminance / 1000F;

        // Retain the float setup rounding used by libjxl for representable ratios. A cold double fallback prevents a tiny
        // positive peak from becoming zero before Log2. Pixel transfer and luminance arithmetic remain single precision.
        float logPeak = relativePeak > 0F ? MathF.Log2(relativePeak) : (float)Math.Log2((double)peakLuminance / 1000D);
        this.SystemGamma = 1.2F * MathF.Pow(1.111F, logPeak);
        float inverseGamma = (1F / 1.2F) * MathF.Pow(1.111F, -logPeak);
        Matrix4x4 matrix = this.RgbToCieXyzMatrix;
        Vector3 luminance = new(matrix.M12, matrix.M22, matrix.M32);
        this.expand = new(new OotfOperator(this.SystemGamma, luminance));
        this.compress = new(new OotfOperator(inverseGamma, luminance));
    }

    /// <summary>
    /// Gets the display peak luminance, in cd/m².
    /// </summary>
    public float PeakLuminance { get; }

    /// <summary>
    /// Gets the display system gamma: 1.2 × 1.111 raised to log2(PeakLuminance / 1000).
    /// Luminance adjustment is identity when this value differs from one by at most 0.01.
    /// </summary>
    public float SystemGamma { get; }

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors) => Compand(vectors, this.expand);

    /// <inheritdoc/>
    public override void Expand(Span<Vector3> vectors) => Compand(vectors, this.expand);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector) => Compand(vector, this.expand);

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors) => Compand(vectors, this.compress);

    /// <inheritdoc/>
    public override void Compress(Span<Vector3> vectors) => Compand(vectors, this.compress);

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector) => Compand(vector, this.compress);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is HlgWorkingSpace other
        && this.PeakLuminance.Equals(other.PeakLuminance)
        && this.WhitePoint.Equals(other.WhitePoint)
        && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        typeof(HlgWorkingSpace),
        this.WhitePoint,
        this.ChromaticityCoordinates,
        this.PeakLuminance);

    /// <summary>
    /// Converts packed RGB triples.
    /// </summary>
    /// <typeparam name="TOperator">The prepared RGB operation.</typeparam>
    /// <param name="vectors">The caller-owned colors. Conversion is in place.</param>
    /// <param name="operation">The prepared component and luminance operation.</param>
    private static void Compand<TOperator>(Span<Vector3> vectors, TOperator operation)
        where TOperator : struct, IColorOperator
    {
        ref float target = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(vectors));
        int i = 0;

        // Each component register holds consecutive pixels. Load the entire group before overwriting its packed source.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (vectors.Length - i).Vector512Count<float>();

            for (nuint group = 0; group < count; group++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(vectors, i, out Vector512<float> red, out Vector512<float> green, out Vector512<float> blue);
                operation.Invoke(ref red, ref green, ref blue);

                // Bound once, after both the component curve and the luminance adjustment. Alpha never enters either operation.
                red = CompandingUtilities.Compand(red, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                green = CompandingUtilities.Compand(green, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                blue = CompandingUtilities.Compand(blue, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                SimdUtils.Interleave3(red, green, blue, ref Unsafe.Add(ref target, (nuint)i * 3));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (vectors.Length - i).Vector256Count<float>();

            for (nuint group = 0; group < count; group++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(vectors, i, out Vector256<float> red, out Vector256<float> green, out Vector256<float> blue);
                operation.Invoke(ref red, ref green, ref blue);

                // Bound once, after both the component curve and the luminance adjustment. Alpha never enters either operation.
                red = CompandingUtilities.Compand(red, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                green = CompandingUtilities.Compand(green, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                blue = CompandingUtilities.Compand(blue, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                SimdUtils.Interleave3(red, green, blue, ref Unsafe.Add(ref target, (nuint)i * 3));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (vectors.Length - i).Vector128Count<float>();

            for (nuint group = 0; group < count; group++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(vectors, i, out Vector128<float> red, out Vector128<float> green, out Vector128<float> blue);
                operation.Invoke(ref red, ref green, ref blue);

                // Bound once, after both the component curve and the luminance adjustment. Alpha never enters either operation.
                red = CompandingUtilities.Compand(red, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                green = CompandingUtilities.Compand(green, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                blue = CompandingUtilities.Compand(blue, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                SimdUtils.Interleave3(red, green, blue, ref Unsafe.Add(ref target, (nuint)i * 3));
            }
        }

        // The tail uses the same RGB operation. It touches only complete colors and retains the original alpha.
        for (; i < vectors.Length; i++)
        {
            Vector3 value = vectors[i];
            Vector4 result = Compand(new Vector4(value, 0F), operation);
            vectors[i] = new Vector3(result.X, result.Y, result.Z);
        }
    }

    /// <summary>
    /// Converts packed RGB colors while preserving alpha.
    /// </summary>
    /// <typeparam name="TOperator">The prepared RGB operation.</typeparam>
    /// <param name="vectors">The caller-owned colors. Conversion is in place.</param>
    /// <param name="operation">The prepared component and luminance operation.</param>
    private static void Compand<TOperator>(Span<Vector4> vectors, TOperator operation)
        where TOperator : struct, IColorOperator
    {
        ref float target = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(vectors));
        int i = 0;

        // Each component register holds consecutive pixels. Load the entire group before overwriting its packed source.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (vectors.Length - i).Vector512Count<float>();

            for (nuint group = 0; group < count; group++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave4(
                    vectors,
                    i,
                    out Vector512<float> red,
                    out Vector512<float> green,
                    out Vector512<float> blue,
                    out Vector512<float> alpha);

                operation.Invoke(ref red, ref green, ref blue);

                // Bound once, after both the component curve and the luminance adjustment. Alpha never enters either operation.
                red = CompandingUtilities.Compand(red, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                green = CompandingUtilities.Compand(green, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                blue = CompandingUtilities.Compand(blue, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                SimdUtils.Interleave4(red, green, blue, alpha, ref Unsafe.Add(ref target, (nuint)i * 4));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (vectors.Length - i).Vector256Count<float>();

            for (nuint group = 0; group < count; group++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave4(
                    vectors,
                    i,
                    out Vector256<float> red,
                    out Vector256<float> green,
                    out Vector256<float> blue,
                    out Vector256<float> alpha);

                operation.Invoke(ref red, ref green, ref blue);

                // Bound once, after both the component curve and the luminance adjustment. Alpha never enters either operation.
                red = CompandingUtilities.Compand(red, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                green = CompandingUtilities.Compand(green, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                blue = CompandingUtilities.Compand(blue, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                SimdUtils.Interleave4(red, green, blue, alpha, ref Unsafe.Add(ref target, (nuint)i * 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (vectors.Length - i).Vector128Count<float>();

            for (nuint group = 0; group < count; group++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave4(
                    vectors,
                    i,
                    out Vector128<float> red,
                    out Vector128<float> green,
                    out Vector128<float> blue,
                    out Vector128<float> alpha);

                operation.Invoke(ref red, ref green, ref blue);

                // Bound once, after both the component curve and the luminance adjustment. Alpha never enters either operation.
                red = CompandingUtilities.Compand(red, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                green = CompandingUtilities.Compand(green, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                blue = CompandingUtilities.Compand(blue, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
                SimdUtils.Interleave4(red, green, blue, alpha, ref Unsafe.Add(ref target, (nuint)i * 4));
            }
        }

        // The tail uses the same RGB operation. It touches only complete colors and retains the original alpha.
        for (; i < vectors.Length; i++)
        {
            vectors[i] = Compand(vectors[i], operation);
        }
    }

    /// <summary>
    /// Converts one RGB color and applies the shared finite result bounds.
    /// </summary>
    /// <typeparam name="TOperator">The prepared RGB operation.</typeparam>
    /// <param name="vector">The input color with unchanged alpha in W.</param>
    /// <param name="operation">The prepared component and luminance operation.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 Compand<TOperator>(Vector4 vector, TOperator operation)
        where TOperator : struct, IColorOperator
    {
        operation.Invoke(ref vector.X, ref vector.Y, ref vector.Z);
        return CompandingUtilities.Compand(vector, default(TensorPrimitives_.IdentityOperator), PixelColorRange.Extended);
    }
}

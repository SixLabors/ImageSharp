// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// Defines the scalar and SIMD mappings used by the shared PNG filter traversal.
/// </summary>
internal interface IPngFilterOperator
{
    /// <summary>
    /// Gets the filter type written to the leading result byte.
    /// </summary>
    public static abstract FilterType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the predictor reads the left component.
    /// </summary>
    public static abstract bool UsesLeft { get; }

    /// <summary>
    /// Gets a value indicating whether the predictor reads the above component.
    /// </summary>
    public static abstract bool UsesAbove { get; }

    /// <summary>
    /// Gets a value indicating whether the predictor reads the upper-left component.
    /// </summary>
    public static abstract bool UsesUpperLeft { get; }

    /// <summary>
    /// Filters one byte from its PNG neighborhood.
    /// </summary>
    /// <param name="scan">The component being filtered.</param>
    /// <param name="left">The corresponding component in the preceding pixel.</param>
    /// <param name="above">The corresponding component in the preceding scanline.</param>
    /// <param name="upperLeft">The preceding component in the preceding scanline.</param>
    /// <returns>The filtered residual.</returns>
    public static abstract byte Invoke(byte scan, byte left, byte above, byte upperLeft);

    /// <summary>
    /// Filters sixteen byte lanes from their PNG neighborhoods.
    /// </summary>
    /// <param name="scan">The components being filtered.</param>
    /// <param name="left">The corresponding components in the preceding pixels.</param>
    /// <param name="above">The corresponding components in the preceding scanline.</param>
    /// <param name="upperLeft">The preceding components in the preceding scanline.</param>
    /// <returns>The filtered residuals.</returns>
    public static abstract Vector128<byte> Invoke(Vector128<byte> scan, Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft);

    /// <summary>
    /// Filters thirty-two byte lanes from their PNG neighborhoods.
    /// </summary>
    /// <param name="scan">The components being filtered.</param>
    /// <param name="left">The corresponding components in the preceding pixels.</param>
    /// <param name="above">The corresponding components in the preceding scanline.</param>
    /// <param name="upperLeft">The preceding components in the preceding scanline.</param>
    /// <returns>The filtered residuals.</returns>
    public static abstract Vector256<byte> Invoke(Vector256<byte> scan, Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft);

    /// <summary>
    /// Filters sixty-four byte lanes from their PNG neighborhoods.
    /// </summary>
    /// <param name="scan">The components being filtered.</param>
    /// <param name="left">The corresponding components in the preceding pixels.</param>
    /// <param name="above">The corresponding components in the preceding scanline.</param>
    /// <param name="upperLeft">The preceding components in the preceding scanline.</param>
    /// <returns>The filtered residuals.</returns>
    public static abstract Vector512<byte> Invoke(Vector512<byte> scan, Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft);
}

/// <summary>
/// Maps each component to its difference from the corresponding component in the preceding pixel.
/// </summary>
internal readonly struct SubFilterOperator : IPngFilterOperator
{
    /// <inheritdoc />
    public static FilterType Type => FilterType.Sub;

    /// <inheritdoc />
    public static bool UsesLeft => true;

    /// <inheritdoc />
    public static bool UsesAbove => false;

    /// <inheritdoc />
    public static bool UsesUpperLeft => false;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static byte Invoke(byte scan, byte left, byte above, byte upperLeft) => (byte)(scan - left);

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector128<byte> Invoke(Vector128<byte> scan, Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft)
        => scan - left;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector256<byte> Invoke(Vector256<byte> scan, Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft)
        => scan - left;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector512<byte> Invoke(Vector512<byte> scan, Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft)
        => scan - left;
}

/// <summary>
/// Maps each component to its difference from the component directly above it.
/// </summary>
internal readonly struct UpFilterOperator : IPngFilterOperator
{
    /// <inheritdoc />
    public static FilterType Type => FilterType.Up;

    /// <inheritdoc />
    public static bool UsesLeft => false;

    /// <inheritdoc />
    public static bool UsesAbove => true;

    /// <inheritdoc />
    public static bool UsesUpperLeft => false;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static byte Invoke(byte scan, byte left, byte above, byte upperLeft) => (byte)(scan - above);

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector128<byte> Invoke(Vector128<byte> scan, Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft)
        => scan - above;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector256<byte> Invoke(Vector256<byte> scan, Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft)
        => scan - above;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector512<byte> Invoke(Vector512<byte> scan, Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft)
        => scan - above;
}

/// <summary>
/// Maps each component to its difference from the truncated average of its left and above neighbors.
/// </summary>
internal readonly struct AverageFilterOperator : IPngFilterOperator
{
    /// <inheritdoc />
    public static FilterType Type => FilterType.Average;

    /// <inheritdoc />
    public static bool UsesLeft => true;

    /// <inheritdoc />
    public static bool UsesAbove => true;

    /// <inheritdoc />
    public static bool UsesUpperLeft => false;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static byte Invoke(byte scan, byte left, byte above, byte upperLeft)
        => (byte)(scan - ((left + above) >> 1));

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector128<byte> Invoke(Vector128<byte> scan, Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft)
    {
        Vector128<byte> average;

        if (Sse2.IsSupported)
        {
            // PAVG rounds upward. Complementing both inputs and the result converts
            // that rounding into the floor((left + above) / 2) required by PNG.
            average = ~Sse2.Average(~left, ~above);
        }
        else if (AdvSimd.IsSupported)
        {
            // ARM's halving add truncates directly and therefore needs no correction.
            average = AdvSimd.FusedAddHalving(left, above);
        }
        else
        {
            // Portable 128-bit backends use the carry-free average identity. Shared
            // bits supply the integer part while differing bits supply half the remainder.
            average = (left & above) + Vector128.ShiftRightLogical(left ^ above, 1);
        }

        return scan - average;
    }

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector256<byte> Invoke(Vector256<byte> scan, Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft)

        // VPAVGB rounds (left + above) / 2 upward. Complementing both inputs and
        // the result changes that to the truncated average required by PNG.
        => scan - ~Avx2.Average(~left, ~above);

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector512<byte> Invoke(Vector512<byte> scan, Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft)

        // AVX-512BW retains VPAVGB's upward rounding, so use the same complement
        // identity as AVX2 to obtain floor((left + above) / 2) in every byte lane.
        => scan - ~Avx512BW.Average(~left, ~above);
}

/// <summary>
/// Maps each component to its difference from the nearest Paeth neighbor.
/// </summary>
internal readonly struct PaethFilterOperator : IPngFilterOperator
{
    /// <inheritdoc />
    public static FilterType Type => FilterType.Paeth;

    /// <inheritdoc />
    public static bool UsesLeft => true;

    /// <inheritdoc />
    public static bool UsesAbove => true;

    /// <inheritdoc />
    public static bool UsesUpperLeft => true;

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static byte Invoke(byte scan, byte left, byte above, byte upperLeft)
    {
        int p = left + above - upperLeft;
        int distanceLeft = Numerics.Abs(p - left);
        int distanceAbove = Numerics.Abs(p - above);
        int distanceUpperLeft = Numerics.Abs(p - upperLeft);

        // PNG resolves equal distances in left, above, upper-left order.
        byte predictor = distanceLeft <= distanceAbove && distanceLeft <= distanceUpperLeft ? left : distanceAbove <= distanceUpperLeft ? above : upperLeft;

        return (byte)(scan - predictor);
    }

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector128<byte> Invoke(Vector128<byte> scan, Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft)
    {
        Vector128<byte> predictor = Predict(left, above, upperLeft);
        return scan - predictor;
    }

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector256<byte> Invoke(Vector256<byte> scan, Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft)
    {
        Vector256<byte> predictor = Predict(left, above, upperLeft);
        return scan - predictor;
    }

    /// <inheritdoc />
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static Vector512<byte> Invoke(Vector512<byte> scan, Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft)
    {
        Vector512<byte> predictor = Predict(left, above, upperLeft);
        return scan - predictor;
    }

    /// <summary>
    /// Selects the nearest Paeth neighbor for sixteen independent byte lanes.
    /// </summary>
    /// <param name="left">The reconstructed component immediately before each current component.</param>
    /// <param name="above">The reconstructed component immediately above each current component.</param>
    /// <param name="upperLeft">The reconstructed component diagonally above and before each current component.</param>
    /// <returns>The selected Paeth predictor for each byte lane.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector128<byte> Predict(Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft)
    {
        // For p = left + above - upperLeft, the Paeth distances simplify to:
        //   distanceLeft  = |above - upperLeft|
        //   distanceAbove = |left - upperLeft|
        // Computing both unsigned subtraction directions and OR-ing them obtains
        // each absolute difference without widening the byte lanes.
        Vector128<byte> aboveMinusUpper = Vector128.SubtractSaturate(above, upperLeft);
        Vector128<byte> leftMinusUpper = Vector128.SubtractSaturate(left, upperLeft);
        Vector128<byte> distanceLeft = Vector128.SubtractSaturate(upperLeft, above) | aboveMinusUpper;
        Vector128<byte> distanceAbove = Vector128.SubtractSaturate(upperLeft, left) | leftMinusUpper;

        return SelectPredictor(left, above, upperLeft, aboveMinusUpper, leftMinusUpper, distanceLeft, distanceAbove);
    }

    /// <summary>
    /// Selects the nearest Paeth neighbor for thirty-two independent byte lanes.
    /// </summary>
    /// <param name="left">The reconstructed component immediately before each current component.</param>
    /// <param name="above">The reconstructed component immediately above each current component.</param>
    /// <param name="upperLeft">The reconstructed component diagonally above and before each current component.</param>
    /// <returns>The selected Paeth predictor for each byte lane.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector256<byte> Predict(Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft)
    {
        // Apply the same Paeth identities as the 128-bit path to thirty-two lanes.
        // Saturating subtraction in both directions forms the absolute differences
        // without widening, preserving one predictor result per source byte.
        Vector256<byte> aboveMinusUpper = Vector256.SubtractSaturate(above, upperLeft);
        Vector256<byte> leftMinusUpper = Vector256.SubtractSaturate(left, upperLeft);
        Vector256<byte> distanceLeft = Vector256.SubtractSaturate(upperLeft, above) | aboveMinusUpper;
        Vector256<byte> distanceAbove = Vector256.SubtractSaturate(upperLeft, left) | leftMinusUpper;

        return SelectPredictor(left, above, upperLeft, aboveMinusUpper, leftMinusUpper, distanceLeft, distanceAbove);
    }

    /// <summary>
    /// Selects the nearest Paeth neighbor for sixty-four independent byte lanes.
    /// </summary>
    /// <param name="left">The reconstructed component immediately before each current component.</param>
    /// <param name="above">The reconstructed component immediately above each current component.</param>
    /// <param name="upperLeft">The reconstructed component diagonally above and before each current component.</param>
    /// <returns>The selected Paeth predictor for each byte lane.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector512<byte> Predict(Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft)
    {
        // Apply the same byte-lane Paeth identities to sixty-four AVX-512BW lanes.
        // No cross-lane operation is required because every component has its own
        // left, above, and upper-left inputs at the matching vector index.
        Vector512<byte> aboveMinusUpper = Vector512.SubtractSaturate(above, upperLeft);
        Vector512<byte> leftMinusUpper = Vector512.SubtractSaturate(left, upperLeft);
        Vector512<byte> distanceLeft = Vector512.SubtractSaturate(upperLeft, above) | aboveMinusUpper;
        Vector512<byte> distanceAbove = Vector512.SubtractSaturate(upperLeft, left) | leftMinusUpper;

        return SelectPredictor(left, above, upperLeft, aboveMinusUpper, leftMinusUpper, distanceLeft, distanceAbove);
    }

    /// <summary>
    /// Applies Paeth distance and tie-breaking rules to sixteen lanes.
    /// </summary>
    /// <param name="left">The left-neighbor candidates.</param>
    /// <param name="above">The above-neighbor candidates.</param>
    /// <param name="upperLeft">The upper-left-neighbor candidates.</param>
    /// <param name="aboveMinusUpper">The saturated differences from above to upper-left.</param>
    /// <param name="leftMinusUpper">The saturated differences from left to upper-left.</param>
    /// <param name="distanceLeft">The Paeth distances for the left candidates.</param>
    /// <param name="distanceAbove">The Paeth distances for the above candidates.</param>
    /// <returns>The selected Paeth predictor for each byte lane.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector128<byte> SelectPredictor(Vector128<byte> left, Vector128<byte> above, Vector128<byte> upperLeft, Vector128<byte> aboveMinusUpper, Vector128<byte> leftMinusUpper, Vector128<byte> distanceLeft, Vector128<byte> distanceAbove)
    {
        Vector128<byte> sameDirection = Vector128.Equals(Vector128.Equals(aboveMinusUpper, Vector128<byte>.Zero), Vector128.Equals(leftMinusUpper, Vector128<byte>.Zero));

        // If left and above lie on the same side of upper-left, distanceUpper is
        // their summed distance and cannot beat either neighbor; the all-bits mask
        // excludes upper-left. On opposite sides, that distance is the absolute
        // difference between distanceLeft and distanceAbove.
        Vector128<byte> distanceUpper = sameDirection | Vector128.SubtractSaturate(distanceAbove, distanceLeft) | Vector128.SubtractSaturate(distanceLeft, distanceAbove);

        // Equality selects above before upper-left, implementing PNG's second tie rule.
        Vector128<byte> minimumAboveUpper = Vector128.Min(distanceUpper, distanceAbove);
        Vector128<byte> aboveOrUpper = Vector128.ConditionalSelect(Vector128.Equals(minimumAboveUpper, distanceAbove), above, upperLeft);

        // Applying the left comparison last preserves PNG's left-first tie rule.
        return Vector128.ConditionalSelect(Vector128.Equals(Vector128.Min(minimumAboveUpper, distanceLeft), distanceLeft), left, aboveOrUpper);
    }

    /// <summary>
    /// Applies Paeth distance and tie-breaking rules to thirty-two lanes.
    /// </summary>
    /// <param name="left">The left-neighbor candidates.</param>
    /// <param name="above">The above-neighbor candidates.</param>
    /// <param name="upperLeft">The upper-left-neighbor candidates.</param>
    /// <param name="aboveMinusUpper">The saturated differences from above to upper-left.</param>
    /// <param name="leftMinusUpper">The saturated differences from left to upper-left.</param>
    /// <param name="distanceLeft">The Paeth distances for the left candidates.</param>
    /// <param name="distanceAbove">The Paeth distances for the above candidates.</param>
    /// <returns>The selected Paeth predictor for each byte lane.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector256<byte> SelectPredictor(Vector256<byte> left, Vector256<byte> above, Vector256<byte> upperLeft, Vector256<byte> aboveMinusUpper, Vector256<byte> leftMinusUpper, Vector256<byte> distanceLeft, Vector256<byte> distanceAbove)
    {
        Vector256<byte> sameDirection = Vector256.Equals(Vector256.Equals(aboveMinusUpper, Vector256<byte>.Zero), Vector256.Equals(leftMinusUpper, Vector256<byte>.Zero));

        // Exclude upper-left when its distance is the non-minimal sum; otherwise
        // compute its distance as the absolute difference of the two known distances.
        Vector256<byte> distanceUpper = sameDirection | Vector256.SubtractSaturate(distanceAbove, distanceLeft) | Vector256.SubtractSaturate(distanceLeft, distanceAbove);

        // Select above on equality, then select left on equality to preserve PNG's
        // required left, above, upper-left tie order in every byte lane.
        Vector256<byte> minimumAboveUpper = Vector256.Min(distanceUpper, distanceAbove);
        Vector256<byte> aboveOrUpper = Vector256.ConditionalSelect(Vector256.Equals(minimumAboveUpper, distanceAbove), above, upperLeft);

        return Vector256.ConditionalSelect(Vector256.Equals(Vector256.Min(minimumAboveUpper, distanceLeft), distanceLeft), left, aboveOrUpper);
    }

    /// <summary>
    /// Applies Paeth distance and tie-breaking rules to sixty-four lanes.
    /// </summary>
    /// <param name="left">The left-neighbor candidates.</param>
    /// <param name="above">The above-neighbor candidates.</param>
    /// <param name="upperLeft">The upper-left-neighbor candidates.</param>
    /// <param name="aboveMinusUpper">The saturated differences from above to upper-left.</param>
    /// <param name="leftMinusUpper">The saturated differences from left to upper-left.</param>
    /// <param name="distanceLeft">The Paeth distances for the left candidates.</param>
    /// <param name="distanceAbove">The Paeth distances for the above candidates.</param>
    /// <returns>The selected Paeth predictor for each byte lane.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector512<byte> SelectPredictor(Vector512<byte> left, Vector512<byte> above, Vector512<byte> upperLeft, Vector512<byte> aboveMinusUpper, Vector512<byte> leftMinusUpper, Vector512<byte> distanceLeft, Vector512<byte> distanceAbove)
    {
        Vector512<byte> sameDirection = Vector512.Equals(Vector512.Equals(aboveMinusUpper, Vector512<byte>.Zero), Vector512.Equals(leftMinusUpper, Vector512<byte>.Zero));

        // Exclude upper-left when its distance is the non-minimal sum; otherwise
        // compute its distance as the absolute difference of the two known distances.
        Vector512<byte> distanceUpper = sameDirection | Vector512.SubtractSaturate(distanceAbove, distanceLeft) | Vector512.SubtractSaturate(distanceLeft, distanceAbove);

        // Select above on equality, then select left on equality to preserve PNG's
        // required left, above, upper-left tie order in every byte lane.
        Vector512<byte> minimumAboveUpper = Vector512.Min(distanceUpper, distanceAbove);
        Vector512<byte> aboveOrUpper = Vector512.ConditionalSelect(Vector512.Equals(minimumAboveUpper, distanceAbove), above, upperLeft);

        return Vector512.ConditionalSelect(Vector512.Equals(Vector512.Min(minimumAboveUpper, distanceLeft), distanceLeft), left, aboveOrUpper);
    }
}

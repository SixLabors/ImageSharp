// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Reconstructs AV1 directional intra-prediction blocks from prepared neighboring samples.
/// </summary>
/// <remarks>
/// The three projection zones implement the directional prediction process in section 7.11.2.4 of the AV1 specification.
/// </remarks>
internal static partial class Av1DirectionalIntraPredictor
{
    /// <summary>
    /// The largest number of samples required to transpose a directional prediction block.
    /// </summary>
    public const int ScratchLength = 64 * 64;

    /// <summary>
    /// Gets the Q8 directional derivatives indexed by acute prediction angle.
    /// </summary>
    private static ReadOnlySpan<int> DirectionalIntraDerivative =>
    [

        // Zero entries represent angles which AV1 never signals. Direct indexing avoids a search or division in
        // each directional block while retaining the exact fixed-point projections from the normative table.
        0, 0, 0, 1023, 0, 0, 547, 0, 0, 372, 0, 0, 0, 0, 273, 0, 0, 215, 0, 0, 178, 0, 0,
        151, 0, 0, 132, 0, 0, 116, 0, 0, 102, 0, 0, 0, 90, 0, 0, 80, 0, 0, 71, 0, 0, 64, 0, 0,
        57, 0, 0, 51, 0, 0, 45, 0, 0, 0, 40, 0, 0, 35, 0, 0, 31, 0, 0, 27, 0, 0, 23, 0, 0,
        19, 0, 0, 15, 0, 0, 0, 0, 11, 0, 0, 7, 0, 0, 3, 0, 0,
    ];

    /// <summary>
    /// Predicts an 8-bit directional block using the widest available SIMD path.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="above">The prepared top reference, including any required extension.</param>
    /// <param name="left">The prepared left reference, including any required extension.</param>
    /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
    /// <param name="angle">The adjusted prediction angle.</param>
    /// <param name="scratch">The caller-owned block transposition workspace.</param>
    public static void Predict(Span<byte> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, bool upsampleAbove, bool upsampleLeft, int angle, Span<byte> scratch)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        if (angle is > 0 and < 90)
        {
            PredictZone1(destination, destinationStride, above, upsampleAbove, GetDeltaX(angle), width, height);
        }
        else if (angle is > 90 and < 180)
        {
            PredictZone2(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, GetDeltaX(angle), GetDeltaY(angle), width, height);
        }
        else if (angle is > 180 and < 270)
        {
            // libaom computes zone 3 as a zone 1 block with swapped dimensions, then transposes it. This preserves
            // contiguous reference reads and destination stores in both hot stages instead of scattering columns.
            Span<byte> transposed = scratch[..(width * height)];
            PredictZone1(transposed, height, left, upsampleLeft, GetDeltaY(angle), height, width);
            Transpose(transposed, destination, height, width, destinationStride);
        }
        else
        {
            Av1PredictionMode mode = angle == 90 ? Av1PredictionMode.Vertical : Av1PredictionMode.Horizontal;
            Av1IntraPredictorBase.GetPredictor(mode).Predict(destination, destinationStride, above, left, width, height);
        }
    }

    /// <summary>
    /// Predicts a high-bit-depth directional block using the widest available SIMD path.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="above">The prepared top reference, including any required extension.</param>
    /// <param name="left">The prepared left reference, including any required extension.</param>
    /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
    /// <param name="angle">The adjusted prediction angle.</param>
    /// <param name="scratch">The caller-owned block transposition workspace.</param>
    public static void Predict(Span<short> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<short> above, ReadOnlySpan<short> left, bool upsampleAbove, bool upsampleLeft, int angle, Span<short> scratch)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        if (angle is > 0 and < 90)
        {
            PredictZone1(destination, destinationStride, above, upsampleAbove, GetDeltaX(angle), width, height);
        }
        else if (angle is > 90 and < 180)
        {
            PredictZone2(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, GetDeltaX(angle), GetDeltaY(angle), width, height);
        }
        else if (angle is > 180 and < 270)
        {
            Span<short> transposed = scratch[..(width * height)];
            PredictZone1(transposed, height, left, upsampleLeft, GetDeltaY(angle), height, width);
            Transpose(transposed, destination, height, width, destinationStride);
        }
        else
        {
            Av1PredictionMode mode = angle == 90 ? Av1PredictionMode.Vertical : Av1PredictionMode.Horizontal;
            Av1IntraPredictorBase.GetPredictor(mode).Predict(destination, destinationStride, above, left, width, height);
        }
    }

    /// <summary>
    /// Predicts an 8-bit directional block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="above">The prepared top reference, including any required extension.</param>
    /// <param name="left">The prepared left reference, including any required extension.</param>
    /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
    /// <param name="angle">The adjusted prediction angle.</param>
    public static void PredictScalar(Span<byte> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, bool upsampleAbove, bool upsampleLeft, int angle)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        if (angle is > 0 and < 90)
        {
            PredictZone1Scalar(destination, destinationStride, above, upsampleAbove, GetDeltaX(angle), width, height);
        }
        else if (angle is > 90 and < 180)
        {
            PredictZone2Scalar(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, GetDeltaX(angle), GetDeltaY(angle), width, height);
        }
        else if (angle is > 180 and < 270)
        {
            PredictZone3Scalar(destination, destinationStride, left, upsampleLeft, GetDeltaY(angle), width, height);
        }
        else
        {
            Av1PredictionMode mode = angle == 90 ? Av1PredictionMode.Vertical : Av1PredictionMode.Horizontal;
            Av1IntraPredictorBase.GetPredictor(mode).PredictScalar(destination, destinationStride, above, left, width, height);
        }
    }

    /// <summary>
    /// Predicts a high-bit-depth directional block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="above">The prepared top reference, including any required extension.</param>
    /// <param name="left">The prepared left reference, including any required extension.</param>
    /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
    /// <param name="angle">The adjusted prediction angle.</param>
    public static void PredictScalar(Span<short> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<short> above, ReadOnlySpan<short> left, bool upsampleAbove, bool upsampleLeft, int angle)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        if (angle is > 0 and < 90)
        {
            PredictZone1Scalar(destination, destinationStride, above, upsampleAbove, GetDeltaX(angle), width, height);
        }
        else if (angle is > 90 and < 180)
        {
            PredictZone2Scalar(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, GetDeltaX(angle), GetDeltaY(angle), width, height);
        }
        else if (angle is > 180 and < 270)
        {
            PredictZone3Scalar(destination, destinationStride, left, upsampleLeft, GetDeltaY(angle), width, height);
        }
        else
        {
            Av1PredictionMode mode = angle == 90 ? Av1PredictionMode.Vertical : Av1PredictionMode.Horizontal;
            Av1IntraPredictorBase.GetPredictor(mode).PredictScalar(destination, destinationStride, above, left, width, height);
        }
    }

    /// <summary>
    /// Gets the horizontal Q8 projection derivative for an adjusted angle.
    /// </summary>
    /// <param name="angle">The adjusted prediction angle.</param>
    /// <returns>The horizontal derivative, or one when the selected zone does not consume it.</returns>
    public static int GetDeltaX(int angle)
        => angle switch
        {
            > 0 and < 90 => DirectionalIntraDerivative[angle],
            > 90 and < 180 => DirectionalIntraDerivative[180 - angle],
            _ => 1,
        };

    /// <summary>
    /// Gets the vertical Q8 projection derivative for an adjusted angle.
    /// </summary>
    /// <param name="angle">The adjusted prediction angle.</param>
    /// <returns>The vertical derivative, or one when the selected zone does not consume it.</returns>
    public static int GetDeltaY(int angle)
        => angle switch
        {
            > 90 and < 180 => DirectionalIntraDerivative[angle - 90],
            > 180 and < 270 => DirectionalIntraDerivative[270 - angle],
            _ => 1,
        };

    /// <summary>
    /// Predicts one 8-bit zone 1 block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="above">The projected top reference.</param>
    /// <param name="upsample">Whether the reference contains half-sample positions.</param>
    /// <param name="derivative">The Q8 projection derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone1Scalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, bool upsample, int derivative, int width, int height)
    {
        int upsampleShift = upsample ? 1 : 0;
        int maximumBasis = (width + height - 1) << upsampleShift;
        int fractionBits = 6 - upsampleShift;
        int basisIncrement = 1 << upsampleShift;
        int projection = derivative;
        ref byte aboveBase = ref Unsafe.AsRef(in above[0]);

        for (int row = 0; row < height; row++, projection += derivative)
        {
            int basis = projection >> fractionBits;
            int weight = ((projection << upsampleShift) & 0x3F) >> 1;
            ref byte destinationRow = ref destination[row * destinationStride];

            for (int column = 0; column < width; column++, basis += basisIncrement)
            {
                Unsafe.Add(ref destinationRow, column) = basis < maximumBasis
                    ? (byte)(((Unsafe.Add(ref aboveBase, basis) * (32 - weight)) + (Unsafe.Add(ref aboveBase, basis + 1) * weight) + 16) >> 5)
                    : Unsafe.Add(ref aboveBase, maximumBasis);
            }
        }
    }

    /// <summary>
    /// Predicts one high-bit-depth zone 1 block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="above">The projected top reference.</param>
    /// <param name="upsample">Whether the reference contains half-sample positions.</param>
    /// <param name="derivative">The Q8 projection derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone1Scalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, bool upsample, int derivative, int width, int height)
    {
        int upsampleShift = upsample ? 1 : 0;
        int maximumBasis = (width + height - 1) << upsampleShift;
        int fractionBits = 6 - upsampleShift;
        int basisIncrement = 1 << upsampleShift;
        int projection = derivative;
        ref short aboveBase = ref Unsafe.AsRef(in above[0]);

        for (int row = 0; row < height; row++, projection += derivative)
        {
            int basis = projection >> fractionBits;
            int weight = ((projection << upsampleShift) & 0x3F) >> 1;
            ref short destinationRow = ref destination[row * destinationStride];

            for (int column = 0; column < width; column++, basis += basisIncrement)
            {
                Unsafe.Add(ref destinationRow, column) = basis < maximumBasis
                    ? (short)(((Unsafe.Add(ref aboveBase, basis) * (32 - weight)) + (Unsafe.Add(ref aboveBase, basis + 1) * weight) + 16) >> 5)
                    : Unsafe.Add(ref aboveBase, maximumBasis);
            }
        }
    }

    /// <summary>
    /// Predicts one 8-bit zone 2 block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="above">The projected top reference.</param>
    /// <param name="left">The projected left reference.</param>
    /// <param name="upsampleAbove">Whether the top reference contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left reference contains half-sample positions.</param>
    /// <param name="dx">The horizontal Q8 derivative.</param>
    /// <param name="dy">The vertical Q8 derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone2Scalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, bool upsampleAbove, bool upsampleLeft, int dx, int dy, int width, int height)
    {
        int aboveShift = upsampleAbove ? 1 : 0;
        int leftShift = upsampleLeft ? 1 : 0;
        int minimumTopBasis = -(1 << aboveShift);
        int topFractionBits = 6 - aboveShift;
        int leftFractionBits = 6 - leftShift;
        int topBasisIncrement = 1 << aboveShift;
        int topProjection = -dx;
        ref byte aboveBase = ref Unsafe.AsRef(in above[0]);
        ref byte leftBase = ref Unsafe.AsRef(in left[0]);

        for (int row = 0; row < height; row++, topProjection -= dx)
        {
            int topBasis = topProjection >> topFractionBits;
            int topWeight = ((topProjection << aboveShift) & 0x3F) >> 1;
            int leftProjection = (row << 6) - dy;
            ref byte destinationRow = ref destination[row * destinationStride];

            for (int column = 0; column < width; column++, topBasis += topBasisIncrement, leftProjection -= dy)
            {
                int prediction;
                if (topBasis >= minimumTopBasis)
                {
                    prediction = (Unsafe.Add(ref aboveBase, topBasis) * (32 - topWeight)) + (Unsafe.Add(ref aboveBase, topBasis + 1) * topWeight);
                }
                else
                {
                    int leftBasis = leftProjection >> leftFractionBits;
                    int leftWeight = ((leftProjection << leftShift) & 0x3F) >> 1;
                    prediction = (Unsafe.Add(ref leftBase, leftBasis) * (32 - leftWeight)) + (Unsafe.Add(ref leftBase, leftBasis + 1) * leftWeight);
                }

                Unsafe.Add(ref destinationRow, column) = (byte)((prediction + 16) >> 5);
            }
        }
    }

    /// <summary>
    /// Predicts one high-bit-depth zone 2 block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="above">The projected top reference.</param>
    /// <param name="left">The projected left reference.</param>
    /// <param name="upsampleAbove">Whether the top reference contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left reference contains half-sample positions.</param>
    /// <param name="dx">The horizontal Q8 derivative.</param>
    /// <param name="dy">The vertical Q8 derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone2Scalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, bool upsampleAbove, bool upsampleLeft, int dx, int dy, int width, int height)
    {
        int aboveShift = upsampleAbove ? 1 : 0;
        int leftShift = upsampleLeft ? 1 : 0;
        int minimumTopBasis = -(1 << aboveShift);
        int topFractionBits = 6 - aboveShift;
        int leftFractionBits = 6 - leftShift;
        int topBasisIncrement = 1 << aboveShift;
        int topProjection = -dx;
        ref short aboveBase = ref Unsafe.AsRef(in above[0]);
        ref short leftBase = ref Unsafe.AsRef(in left[0]);

        for (int row = 0; row < height; row++, topProjection -= dx)
        {
            int topBasis = topProjection >> topFractionBits;
            int topWeight = ((topProjection << aboveShift) & 0x3F) >> 1;
            int leftProjection = (row << 6) - dy;
            ref short destinationRow = ref destination[row * destinationStride];

            for (int column = 0; column < width; column++, topBasis += topBasisIncrement, leftProjection -= dy)
            {
                int prediction;
                if (topBasis >= minimumTopBasis)
                {
                    prediction = (Unsafe.Add(ref aboveBase, topBasis) * (32 - topWeight)) + (Unsafe.Add(ref aboveBase, topBasis + 1) * topWeight);
                }
                else
                {
                    int leftBasis = leftProjection >> leftFractionBits;
                    int leftWeight = ((leftProjection << leftShift) & 0x3F) >> 1;
                    prediction = (Unsafe.Add(ref leftBase, leftBasis) * (32 - leftWeight)) + (Unsafe.Add(ref leftBase, leftBasis + 1) * leftWeight);
                }

                Unsafe.Add(ref destinationRow, column) = (short)((prediction + 16) >> 5);
            }
        }
    }

    /// <summary>
    /// Predicts one 8-bit zone 3 block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="left">The projected left reference.</param>
    /// <param name="upsample">Whether the reference contains half-sample positions.</param>
    /// <param name="derivative">The Q8 projection derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone3Scalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> left, bool upsample, int derivative, int width, int height)
    {
        int upsampleShift = upsample ? 1 : 0;
        int maximumBasis = (width + height - 1) << upsampleShift;
        int fractionBits = 6 - upsampleShift;
        int basisIncrement = 1 << upsampleShift;
        int projection = derivative;
        ref byte leftBase = ref Unsafe.AsRef(in left[0]);

        for (int column = 0; column < width; column++, projection += derivative)
        {
            int basis = projection >> fractionBits;
            int weight = ((projection << upsampleShift) & 0x3F) >> 1;
            for (int row = 0; row < height; row++, basis += basisIncrement)
            {
                destination[(row * destinationStride) + column] = basis < maximumBasis
                    ? (byte)(((Unsafe.Add(ref leftBase, basis) * (32 - weight)) + (Unsafe.Add(ref leftBase, basis + 1) * weight) + 16) >> 5)
                    : Unsafe.Add(ref leftBase, maximumBasis);
            }
        }
    }

    /// <summary>
    /// Predicts one high-bit-depth zone 3 block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="left">The projected left reference.</param>
    /// <param name="upsample">Whether the reference contains half-sample positions.</param>
    /// <param name="derivative">The Q8 projection derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone3Scalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> left, bool upsample, int derivative, int width, int height)
    {
        int upsampleShift = upsample ? 1 : 0;
        int maximumBasis = (width + height - 1) << upsampleShift;
        int fractionBits = 6 - upsampleShift;
        int basisIncrement = 1 << upsampleShift;
        int projection = derivative;
        ref short leftBase = ref Unsafe.AsRef(in left[0]);

        for (int column = 0; column < width; column++, projection += derivative)
        {
            int basis = projection >> fractionBits;
            int weight = ((projection << upsampleShift) & 0x3F) >> 1;
            for (int row = 0; row < height; row++, basis += basisIncrement)
            {
                destination[(row * destinationStride) + column] = basis < maximumBasis
                    ? (short)(((Unsafe.Add(ref leftBase, basis) * (32 - weight)) + (Unsafe.Add(ref leftBase, basis + 1) * weight) + 16) >> 5)
                    : Unsafe.Add(ref leftBase, maximumBasis);
            }
        }
    }
}

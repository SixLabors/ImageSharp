// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Implements AV1 intra prediction for 10-bit and 12-bit sample buffers.
/// </summary>
/// <remarks>
/// Samples are stored in signed 16-bit buffers, but predictions are clamped to the nonnegative range of the signaled bit depth.
/// </remarks>
internal static class Av1HighBitDepthPredictor
{
    /// <summary>
    /// The row stride of the temporary filter intra prediction buffer.
    /// </summary>
    private const int FilterBufferStride = 33;

    /// <summary>
    /// The number of samples in the temporary filter intra prediction buffer.
    /// </summary>
    private const int FilterBufferLength = FilterBufferStride * FilterBufferStride;

    /// <summary>
    /// The number of nonzero filter coefficients used to predict each filter intra sample.
    /// </summary>
    private const int FilterTapsPerPixel = 7;

    /// <summary>
    /// The number of samples produced by each filter coefficient group.
    /// </summary>
    private const int FilterPixelsPerGroup = 8;

    /// <summary>
    /// The number of stored coefficients for each filter intra mode.
    /// </summary>
    private const int FilterTapsPerMode = FilterTapsPerPixel * FilterPixelsPerGroup;

    /// <summary>
    /// Produces a high-bit-depth DC intra prediction from the available neighboring samples.
    /// </summary>
    /// <param name="hasLeft">A value indicating whether reconstructed left samples are available.</param>
    /// <param name="hasAbove">A value indicating whether reconstructed top samples are available.</param>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    public static void DcPredictor(
        bool hasLeft,
        bool hasAbove,
        Av1TransformSize transformSize,
        Span<short> destination,
        nuint destinationStride,
        Span<short> above,
        Span<short> left,
        int bitDepth)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int sum = 0;
        int count = 0;

        if (hasAbove)
        {
            for (int column = 0; column < width; column++)
            {
                sum += above[column];
            }

            count += width;
        }

        if (hasLeft)
        {
            for (int row = 0; row < height; row++)
            {
                sum += left[row];
            }

            count += height;
        }

        short prediction = count == 0
            ? (short)(1 << (bitDepth - 1))
            : (short)((sum + (count >> 1)) / count);

        // The midpoint is normative when neither edge exists; otherwise the half-count bias
        // rounds the mean of the available top and left samples to the nearest integer.
        Fill(destination, destinationStride, width, height, prediction);
    }

    /// <summary>
    /// Produces a high-bit-depth nondirectional intra prediction.
    /// </summary>
    /// <param name="mode">The nondirectional prediction mode to apply.</param>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    public static void GeneralPredictor(
        Av1PredictionMode mode,
        Av1TransformSize transformSize,
        Span<short> destination,
        nuint destinationStride,
        Span<short> above,
        Span<short> left)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        switch (mode)
        {
            case Av1PredictionMode.Horizontal:
                PredictHorizontal(destination, destinationStride, left, width, height);
                break;
            case Av1PredictionMode.Vertical:
                PredictVertical(destination, destinationStride, above, width, height);
                break;
            case Av1PredictionMode.Paeth:
                PredictPaeth(destination, destinationStride, above, left, width, height);
                break;
            case Av1PredictionMode.Smooth:
                PredictSmooth(destination, destinationStride, above, left, width, height);
                break;
            case Av1PredictionMode.SmoothHorizontal:
                PredictSmoothHorizontal(destination, destinationStride, above, left, width, height);
                break;
            case Av1PredictionMode.SmoothVertical:
                PredictSmoothVertical(destination, destinationStride, above, left, width, height);
                break;
        }
    }

    /// <summary>
    /// Produces a high-bit-depth directional intra prediction.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    /// <param name="above">The top reference samples, including any required extension.</param>
    /// <param name="left">The left reference samples, including any required extension.</param>
    /// <param name="upsampleAbove">A value indicating whether the top reference samples were upsampled.</param>
    /// <param name="upsampleLeft">A value indicating whether the left reference samples were upsampled.</param>
    /// <param name="angle">The prediction angle in degrees.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    public static void DirectionalPredictor(
        Span<short> destination,
        nuint destinationStride,
        Av1TransformSize transformSize,
        Span<short> above,
        Span<short> left,
        bool upsampleAbove,
        bool upsampleLeft,
        int angle,
        int bitDepth)
    {
        Guard.MustBeBetweenOrEqualTo(angle, 1, 269, nameof(angle));
        int dx = Av1PredictorFactory.GetDeltaX(angle);
        int dy = Av1PredictorFactory.GetDeltaY(angle);
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Angles on a cardinal axis copy one reference edge directly. Other angles are
        // separated into the three AV1 projection zones according to the edges they cross.
        if (angle is > 0 and < 90)
        {
            PredictDirectionalZone1(destination, destinationStride, above, upsampleAbove, dx, width, height, bitDepth);
        }
        else if (angle is > 90 and < 180)
        {
            PredictDirectionalZone2(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, dx, dy, width, height, bitDepth);
        }
        else if (angle is > 180 and < 270)
        {
            PredictDirectionalZone3(destination, destinationStride, left, upsampleLeft, dx, dy, width, height, bitDepth);
        }
        else if (angle == 90)
        {
            PredictVertical(destination, destinationStride, above, width, height);
        }
        else if (angle == 180)
        {
            PredictHorizontal(destination, destinationStride, left, width, height);
        }
    }

    /// <summary>
    /// Produces a high-bit-depth filter intra prediction.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="mode">The filter intra mode whose coefficient set is applied.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    public static void FilterIntraPredictor(
        Span<short> destination,
        nuint destinationStride,
        Av1TransformSize transformSize,
        Span<short> above,
        Span<short> left,
        Av1FilterIntraMode mode,
        int bitDepth)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int maximum = (1 << bitDepth) - 1;
        DebugGuard.MustBeLessThanOrEqualTo(width, 32, nameof(width));
        DebugGuard.MustBeLessThanOrEqualTo(height, 32, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(destinationStride, (nuint)width, nameof(destinationStride));
        Guard.MustBeSizedAtLeast(destination, (int)destinationStride * height, nameof(destination));
        Guard.MustBeSizedAtLeast(above, width, nameof(above));
        Guard.MustBeSizedAtLeast(left, height, nameof(left));

        Span<short> buffer = stackalloc short[FilterBufferLength];
        ref short bufferRef = ref buffer[0];
        ref short aboveRef = ref above[0];
        ref short leftRef = ref left[0];

        // Row zero includes the top-left sample followed by the top neighbors.
        // Column zero stores the left neighbors so each 4-by-2 group can consume the
        // seven already-reconstructed samples defined by the recursive AV1 process.
        bufferRef = Unsafe.Subtract(ref aboveRef, 1);
        above[..width].CopyTo(buffer[1..]);
        for (int row = 0; row < height; row++)
        {
            Unsafe.Add(ref bufferRef, (row + 1) * FilterBufferStride) = Unsafe.Add(ref leftRef, row);
        }

        int modeOffset = (int)mode * FilterTapsPerMode;
        ref sbyte tapsRef = ref Av1FilterIntraPredictor.Taps[modeOffset];
        for (int row = 1; row <= height; row += 2)
        {
            for (int column = 1; column <= width; column += 4)
            {
                int sourceOffset = ((row - 1) * FilterBufferStride) + column - 1;
                int p0 = Unsafe.Add(ref bufferRef, sourceOffset);
                int p1 = Unsafe.Add(ref bufferRef, sourceOffset + 1);
                int p2 = Unsafe.Add(ref bufferRef, sourceOffset + 2);
                int p3 = Unsafe.Add(ref bufferRef, sourceOffset + 3);
                int p4 = Unsafe.Add(ref bufferRef, sourceOffset + 4);
                int p5 = Unsafe.Add(ref bufferRef, sourceOffset + FilterBufferStride);
                int p6 = Unsafe.Add(ref bufferRef, sourceOffset + (2 * FilterBufferStride));

                for (int pixel = 0; pixel < FilterPixelsPerGroup; pixel++)
                {
                    int tapOffset = pixel * FilterTapsPerPixel;
                    int prediction =
                        (Unsafe.Add(ref tapsRef, tapOffset) * p0)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 1) * p1)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 2) * p2)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 3) * p3)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 4) * p4)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 5) * p5)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 6) * p6);

                    int rowOffset = pixel >> 2;
                    int columnOffset = pixel & 3;
                    int destinationOffset = ((row + rowOffset) * FilterBufferStride) + column + columnOffset;
                    Unsafe.Add(ref bufferRef, destinationOffset) =
                        (short)Av1Math.Clamp(Av1Math.RoundPowerOf2(prediction, 4), 0, maximum);
                }
            }
        }

        for (int row = 0; row < height; row++)
        {
            buffer.Slice(((row + 1) * FilterBufferStride) + 1, width).CopyTo(
                destination.Slice(row * (int)destinationStride, width));
        }
    }

    /// <summary>
    /// Copies each left reference sample across one destination row.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    private static void PredictHorizontal(Span<short> destination, nuint stride, Span<short> left, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            destination.Slice(row * (int)stride, width).Fill(left[row]);
        }
    }

    /// <summary>
    /// Copies the top reference samples into every destination row.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    private static void PredictVertical(Span<short> destination, nuint stride, Span<short> above, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            above[..width].CopyTo(destination.Slice(row * (int)stride, width));
        }
    }

    /// <summary>
    /// Produces a Paeth prediction from the nearest top, left, and top-left reference sample.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    private static void PredictPaeth(Span<short> destination, nuint stride, Span<short> above, Span<short> left, int width, int height)
    {
        int topLeft = Unsafe.Subtract(ref above[0], 1);
        for (int row = 0; row < height; row++)
        {
            Span<short> destinationRow = destination.Slice(row * (int)stride, width);
            for (int column = 0; column < width; column++)
            {
                short leftValue = left[row];
                short topValue = above[column];
                int basis = topValue + leftValue - topLeft;
                int leftDistance = Av1Math.AbsoluteDifference(basis, leftValue);
                int topDistance = Av1Math.AbsoluteDifference(basis, topValue);
                int topLeftDistance = Av1Math.AbsoluteDifference(basis, topLeft);

                // The comparison order preserves AV1's left, top, then top-left tie precedence.
                destinationRow[column] = leftDistance <= topDistance && leftDistance <= topLeftDistance
                    ? leftValue
                    : topDistance <= topLeftDistance ? topValue : (short)topLeft;
            }
        }
    }

    /// <summary>
    /// Produces a two-dimensional smooth prediction from the four terminating edge samples.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    private static void PredictSmooth(Span<short> destination, nuint stride, Span<short> above, Span<short> left, int width, int height)
    {
        int below = left[height - 1];
        int right = above[width - 1];
        Span<int> widthWeights = Av1SmoothPredictor.Weights.AsSpan(width, width);
        Span<int> heightWeights = Av1SmoothPredictor.Weights.AsSpan(height, height);
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;
        int log2Scale = Av1SmoothPredictor.WeightLog2Scale + 1;

        // Horizontal weights follow the block width and vertical weights follow the height.
        // Keeping those domains separate is required for rectangular transform blocks.
        for (int row = 0; row < height; row++)
        {
            int rowWeight = heightWeights[row];
            Span<short> destinationRow = destination.Slice(row * (int)stride, width);
            for (int column = 0; column < width; column++)
            {
                int columnWeight = widthWeights[column];
                int prediction = (above[column] * rowWeight) + (below * (scale - rowWeight));
                prediction += (left[row] * columnWeight) + (right * (scale - columnWeight));
                destinationRow[column] = (short)Av1Math.DivideRound(prediction, log2Scale);
            }
        }
    }

    /// <summary>
    /// Produces a horizontal smooth prediction between the left and right edge samples.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    private static void PredictSmoothHorizontal(Span<short> destination, nuint stride, Span<short> above, Span<short> left, int width, int height)
    {
        int right = above[width - 1];
        Span<int> weights = Av1SmoothPredictor.Weights.AsSpan(width, width);
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;

        for (int row = 0; row < height; row++)
        {
            Span<short> destinationRow = destination.Slice(row * (int)stride, width);
            for (int column = 0; column < width; column++)
            {
                int weight = weights[column];
                int prediction = (left[row] * weight) + (right * (scale - weight));
                destinationRow[column] = (short)Av1Math.DivideRound(prediction, Av1SmoothPredictor.WeightLog2Scale);
            }
        }
    }

    /// <summary>
    /// Produces a vertical smooth prediction between the top and bottom edge samples.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The reconstructed top reference samples.</param>
    /// <param name="left">The reconstructed left reference samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    private static void PredictSmoothVertical(Span<short> destination, nuint stride, Span<short> above, Span<short> left, int width, int height)
    {
        int below = left[height - 1];
        Span<int> weights = Av1SmoothPredictor.Weights.AsSpan(height, height);
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;

        for (int row = 0; row < height; row++)
        {
            int weight = weights[row];
            Span<short> destinationRow = destination.Slice(row * (int)stride, width);
            for (int column = 0; column < width; column++)
            {
                int prediction = (above[column] * weight) + (below * (scale - weight));
                destinationRow[column] = (short)Av1Math.DivideRound(prediction, Av1SmoothPredictor.WeightLog2Scale);
            }
        }
    }

    /// <summary>
    /// Projects top reference samples into a directional zone 1 prediction block.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top reference samples, including any required extension.</param>
    /// <param name="upsample">A value indicating whether the top reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative in Q6 precision.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private static void PredictDirectionalZone1(Span<short> destination, nuint stride, Span<short> above, bool upsample, int dx, int width, int height, int bitDepth)
    {
        int upsampleAbove = upsample ? 1 : 0;
        int maxBasisX = (width + height - 1) << upsampleAbove;
        int fractionBitCount = 6 - upsampleAbove;
        int basisIncrement = 1 << upsampleAbove;
        int maximum = (1 << bitDepth) - 1;
        int x = dx;
        ref short aboveRef = ref above[0];

        for (int row = 0; row < height; row++)
        {
            Span<short> destinationRow = destination.Slice(row * (int)stride, width);

            // AV1 retains six fractional projection bits, or five after reference upsampling.
            // The interpolation weights sum to 32 because the low projection bit is discarded.
            int basis = x >> fractionBitCount;
            int shift = ((x << upsampleAbove) & 0x3F) >> 1;
            for (int column = 0; column < width; column++)
            {
                if (basis < maxBasisX)
                {
                    int prediction = (Unsafe.Add(ref aboveRef, basis) * (32 - shift)) + (Unsafe.Add(ref aboveRef, basis + 1) * shift);
                    destinationRow[column] = (short)Av1Math.Clamp(Av1Math.RoundPowerOf2(prediction, 5), 0, maximum);
                }
                else
                {
                    destinationRow[column] = Unsafe.Add(ref aboveRef, maxBasisX);
                }

                basis += basisIncrement;
            }

            x += dx;
        }
    }

    /// <summary>
    /// Projects top and left reference samples into a directional zone 2 prediction block.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top reference samples, including any required extension.</param>
    /// <param name="left">The left reference samples, including any required extension.</param>
    /// <param name="doUpsampleAbove">A value indicating whether the top reference samples were upsampled.</param>
    /// <param name="doUpsampleLeft">A value indicating whether the left reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative in Q6 precision.</param>
    /// <param name="dy">The vertical projection derivative in Q6 precision.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private static void PredictDirectionalZone2(Span<short> destination, nuint stride, Span<short> above, Span<short> left, bool doUpsampleAbove, bool doUpsampleLeft, int dx, int dy, int width, int height, int bitDepth)
    {
        int upsampleAbove = doUpsampleAbove ? 1 : 0;
        int upsampleLeft = doUpsampleLeft ? 1 : 0;
        int minBasisX = -(1 << upsampleAbove);
        int fractionBitCountX = 6 - upsampleAbove;
        int fractionBitCountY = 6 - upsampleLeft;
        int basisIncrementX = 1 << upsampleAbove;
        int maximum = (1 << bitDepth) - 1;
        int x = -dx;
        ref short aboveRef = ref above[0];
        ref short leftRef = ref left[0];

        for (int row = 0; row < height; row++)
        {
            Span<short> destinationRow = destination.Slice(row * (int)stride, width);
            int basisX = x >> fractionBitCountX;
            int y = (row << 6) - dy;
            for (int column = 0; column < width; column++, basisX += basisIncrementX, y -= dy)
            {
                int prediction;

                // A nonnegative top projection uses the above edge. Once the projection crosses
                // the top-left corner, the same destination sample is projected from the left edge.
                if (basisX >= minBasisX)
                {
                    int shift = ((x * (1 << upsampleAbove)) & 0x3F) >> 1;
                    prediction = (Unsafe.Add(ref aboveRef, basisX) * (32 - shift)) + (Unsafe.Add(ref aboveRef, basisX + 1) * shift);
                }
                else
                {
                    int basisY = y >> fractionBitCountY;
                    int shift = ((y * (1 << upsampleLeft)) & 0x3F) >> 1;
                    prediction = (Unsafe.Add(ref leftRef, basisY) * (32 - shift)) + (Unsafe.Add(ref leftRef, basisY + 1) * shift);
                }

                destinationRow[column] = (short)Av1Math.Clamp(Av1Math.RoundPowerOf2(prediction, 5), 0, maximum);
            }

            x -= dx;
        }
    }

    /// <summary>
    /// Projects left reference samples into a directional zone 3 prediction block.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="left">The left reference samples, including any required extension.</param>
    /// <param name="upsample">A value indicating whether the left reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative, which must be one in zone 3.</param>
    /// <param name="dy">The vertical projection derivative in Q6 precision.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private static void PredictDirectionalZone3(Span<short> destination, nuint stride, Span<short> left, bool upsample, int dx, int dy, int width, int height, int bitDepth)
    {
        int upsampleLeft = upsample ? 1 : 0;
        int maxBasisY = (width + height - 1) << upsampleLeft;
        int fractionBitCount = 6 - upsampleLeft;
        int basisIncrement = 1 << upsampleLeft;
        int maximum = (1 << bitDepth) - 1;
        int y = dy;
        ref short leftRef = ref left[0];
        Guard.IsTrue(dx == 1, nameof(dx), "Dx expected to be always equal to 1 for directional Zone 3 prediction.");

        for (int column = 0; column < width; column++)
        {
            // Zone 3 is the transpose of zone 1: columns advance along the projected left edge,
            // while rows advance through the reference samples for each destination column.
            int basis = y >> fractionBitCount;
            int shift = ((y << upsampleLeft) & 0x3F) >> 1;
            for (int row = 0; row < height; row++)
            {
                int destinationOffset = (row * (int)stride) + column;
                if (basis < maxBasisY)
                {
                    int prediction = (Unsafe.Add(ref leftRef, basis) * (32 - shift)) + (Unsafe.Add(ref leftRef, basis + 1) * shift);
                    destination[destinationOffset] = (short)Av1Math.Clamp(Av1Math.RoundPowerOf2(prediction, 5), 0, maximum);
                }
                else
                {
                    destination[destinationOffset] = Unsafe.Add(ref leftRef, maxBasisY);
                }

                basis += basisIncrement;
            }

            y += dy;
        }
    }

    /// <summary>
    /// Fills a rectangular prediction block with one sample value.
    /// </summary>
    /// <param name="destination">The buffer that receives the sample value.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    /// <param name="value">The sample value written to every destination position.</param>
    private static void Fill(Span<short> destination, nuint stride, int width, int height, short value)
    {
        for (int row = 0; row < height; row++)
        {
            destination.Slice(row * (int)stride, width).Fill(value);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

#pragma warning disable IDE0057 // Use range operator

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Splits modular channels into pair averages and predicted differences, or restores their original samples.
/// </summary>
/// <remarks>
/// Horizontal reconstruction depends on the preceding restored sample. Transposed row tiles put independent rows in SIMD lanes.
/// Vertical reconstruction puts independent columns in SIMD lanes. Both paths preserve the scalar signed division and stored Int32 results.
/// </remarks>
internal static partial class JxlSqueeze
{
    private const int MaxFirstPreviewSize = 8;

    /// <summary>
    /// Computes the squeeze average with its first-sample-dependent rounding.
    /// </summary>
    /// <param name="first">The first original sample.</param>
    /// <param name="second">The second original sample.</param>
    /// <returns>The biased average. The sum uses Int64 so every Int32 pair is representable before division.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Average(int first, int second) => (int)(((long)first + second + (first > second ? 1 : 0)) >> 1);

    /// <summary>
    /// Estimates the difference inside a pair from its predecessor and adjacent averages.
    /// </summary>
    /// <param name="previous">The preceding original or restored odd sample.</param>
    /// <param name="average">The current pair average.</param>
    /// <param name="nextAverage">The following pair average.</param>
    /// <returns>The parity-limited tendency, using native 64-bit arithmetic throughout.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SmoothTendency(long previous, long average, long nextAverage)
    {
        long difference = 0;
        if (previous >= average && average >= nextAverage)
        {
            // Estimate the difference only along a monotonic run. Division truncates toward zero, as in the native scalar decoder.
            difference = ((4 * previous) - (3 * nextAverage) - average + 6) / 12;
            if (difference - (difference & 1) > 2 * (previous - average))
            {
                difference = (2 * (previous - average)) + 1;
            }

            if (difference + (difference & 1) > 2 * (average - nextAverage))
            {
                difference = 2 * (average - nextAverage);
            }
        }
        else if (previous <= average && average <= nextAverage)
        {
            difference = ((4 * previous) - (3 * nextAverage) - average - 6) / 12;
            if (difference + (difference & 1) < 2 * (previous - average))
            {
                difference = (2 * (previous - average)) - 1;
            }

            if (difference - (difference & 1) < 2 * (average - nextAverage))
            {
                difference = 2 * (average - nextAverage);
            }
        }

        // These bounds prevent a predicted pair from extending beyond a monotonic neighborhood.
        // Parity matters because inverse reconstruction divides a signed difference toward zero.
        return difference;
    }

    /// <summary>
    /// Restores a horizontal channel from its average and residual planes, then releases the displaced average storage.
    /// </summary>
    /// <param name="configuration">The configuration used for restored samples.</param>
    /// <param name="input">The owner of the average and residual channels.</param>
    /// <param name="c">The average channel index, which receives the restored samples.</param>
    /// <param name="rc">The residual channel index. InverseSqueeze removes it after reconstruction.</param>
    public static void InverseHorizontalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        DebugGuard.MustBeLessThan(c, input.Channels.Count, nameof(c));

        DebugGuard.MustBeLessThan(rc, input.Channels.Count, nameof(rc));
        JxlModularChannel average = input.Channels[c];
        JxlModularChannel residual = input.Channels[rc];
        if (average.Width != JxlMath.DivCeil(average.Width + residual.Width, 2))
        {
            throw new InvalidOperationException("Invalid width");
        }

        if (average.Height != residual.Height)
        {
            throw new InvalidOperationException("Height of the input channel must be equal to the height of the residual channel");
        }

        if (residual.Width == 0)
        {
            average.HorizontalShift--;
            return;
        }

        using JxlModularChannel output = new(configuration, average.Width + residual.Width, average.Height, average.HorizontalShift - 1, average.VerticalShift);

        // Reconstruction changes channel resolution but retains the represented component.
        output.Component = average.Component;

        if (residual.Height != 0)
        {
            ReadOnlySpan<int> averages = average.GetRow(0);
            ReadOnlySpan<int> residuals = residual.GetRow(0);
            Span<int> restored = output.GetRow(0);

            int averageStride = average.Plane.PixelsPerRow;
            int residualStride = residual.Plane.PixelsPerRow;
            int outputStride = output.Plane.PixelsPerRow;

            // Three 16-by-16 buffers use at most 3 KiB. Every active tile is written before reading; unused capacity is never addressed.
            // Reuse consumed average/residual columns for even/odd output, then reuse dead average storage during the final transpose.
            Unsafe.SkipInit(out InlineArray256<int> averageStorage);
            Unsafe.SkipInit(out InlineArray256<int> residualStorage);
            Unsafe.SkipInit(out InlineArray256<int> rowStorage);

            Span<int> averageTile = averageStorage;
            Span<int> residualTile = residualStorage;
            Span<int> rowTile = rowStorage;
            int y = 0;

            if (Vector512.IsHardwareAccelerated && residual.Width >= Vector512<int>.Count)
            {
                for (; y <= average.Height - Vector512<int>.Count; y += Vector512<int>.Count)
                {
                    InverseHorizontalRows(
                        averages,
                        residuals,
                        restored,
                        averageStride,
                        residualStride,
                        outputStride,
                        average.Width,
                        residual.Width,
                        output.Width,
                        y,
                        Vector512<int>.Count,
                        averageTile,
                        residualTile,
                        rowTile);
                }
            }

            if (Vector256.IsHardwareAccelerated && residual.Width >= Vector256<int>.Count)
            {
                for (; y <= average.Height - Vector256<int>.Count; y += Vector256<int>.Count)
                {
                    InverseHorizontalRows(
                        averages,
                        residuals,
                        restored,
                        averageStride,
                        residualStride,
                        outputStride,
                        average.Width,
                        residual.Width,
                        output.Width,
                        y,
                        Vector256<int>.Count,
                        averageTile,
                        residualTile,
                        rowTile);
                }
            }

            if (Vector128.IsHardwareAccelerated && residual.Width >= Vector128<int>.Count)
            {
                for (; y <= average.Height - Vector128<int>.Count; y += Vector128<int>.Count)
                {
                    InverseHorizontalRows(
                        averages,
                        residuals,
                        restored,
                        averageStride,
                        residualStride,
                        outputStride,
                        average.Width,
                        residual.Width,
                        output.Width,
                        y,
                        Vector128<int>.Count,
                        averageTile,
                        residualTile,
                        rowTile);
                }
            }

            for (; y < average.Height; y++)
            {
                InverseHorizontalRow(
                    averages.Slice(y * averageStride, average.Width),
                    residuals.Slice(y * residualStride, residual.Width),
                    restored.Slice(y * outputStride, output.Width),
                    0);
            }
        }

        // The channel keeps its identity. The temporary receives and releases the old average allocation.
        average.Swap(output);
    }

    /// <summary>
    /// Restores complete horizontal tiles across independent rows and finishes each row's scalar remainder.
    /// </summary>
    /// <param name="averages">The borrowed average allocation from its logical origin.</param>
    /// <param name="residuals">The borrowed residual allocation from its logical origin.</param>
    /// <param name="output">The restored allocation from its logical origin.</param>
    /// <param name="averageStride">The average row stride in integers.</param>
    /// <param name="residualStride">The residual row stride in integers.</param>
    /// <param name="outputStride">The restored row stride in integers.</param>
    /// <param name="averageWidth">The logical average width.</param>
    /// <param name="residualWidth">The logical residual width.</param>
    /// <param name="outputWidth">The logical restored width.</param>
    /// <param name="firstRow">The first row in this group.</param>
    /// <param name="rows">The selected register's lane count: four, eight or sixteen.</param>
    /// <param name="averageTile">Reusable storage for at least rows squared integers.</param>
    /// <param name="residualTile">Reusable storage for at least rows squared integers.</param>
    /// <param name="rowTile">Reusable storage for at least rows squared integers.</param>
    private static void InverseHorizontalRows(
        ReadOnlySpan<int> averages,
        ReadOnlySpan<int> residuals,
        Span<int> output,
        int averageStride,
        int residualStride,
        int outputStride,
        int averageWidth,
        int residualWidth,
        int outputWidth,
        int firstRow,
        int rows,
        Span<int> averageTile,
        Span<int> residualTile,
        Span<int> rowTile)
    {
        int area = rows * rows;
        averageTile = averageTile[..area];
        residualTile = residualTile[..area];
        rowTile = rowTile[..area];
        int x = 0;

        for (; x <= residualWidth - rows; x += rows)
        {
            // Transposed columns put different image rows in SIMD lanes. Horizontal dependency remains between consecutive columns, not lanes.
            SimdUtils.Transpose(averages[((firstRow * averageStride) + x)..], averageStride, averageTile, rows, rows, rows);

            SimdUtils.Transpose(residuals[((firstRow * residualStride) + x)..], residualStride, residualTile, rows, rows, rows);

            // Until arithmetic finishes, the third tile holds only the predecessor and the one-column average lookahead.
            if (x == 0)
            {
                averageTile[..rows].CopyTo(rowTile);
            }
            else
            {
                CopyColumn(output, outputStride, firstRow, (2 * x) - 1, rowTile[..rows]);
            }

            if (x + rows < averageWidth)
            {
                CopyColumn(averages, averageStride, firstRow, x + rows, rowTile.Slice(rows, rows));
            }
            else
            {
                averageTile.Slice((rows - 1) * rows, rows).CopyTo(rowTile.Slice(rows, rows));
            }

            InverseSqueezeOperator operation = default;
            ref int averageStart = ref MemoryMarshal.GetReference(averageTile);

            ref int residualStart = ref MemoryMarshal.GetReference(residualTile);
            ref int neighbors = ref MemoryMarshal.GetReference(rowTile);

            // Select the width outside the dependent column loop. Keep the preceding restored odd samples in a register.
            if (rows == Vector512<int>.Count)
            {
                Vector512<int> previous = Vector512.LoadUnsafe(ref neighbors);

                Vector512<int> current = Vector512.LoadUnsafe(ref averageStart);

                for (int column = 0; column < rows; column++)
                {
                    uint offset = (uint)(column * rows);

                    Vector512<int> next = column + 1 < rows
                        ? Vector512.LoadUnsafe(ref averageStart, offset + (uint)rows)
                        : Vector512.LoadUnsafe(ref neighbors, (uint)rows);

                    operation.Invoke(current, Vector512.LoadUnsafe(ref residualStart, offset), next, previous, out Vector512<int> even, out previous);
                    even.StoreUnsafe(ref averageStart, offset);
                    previous.StoreUnsafe(ref residualStart, offset);
                    current = next;
                }
            }
            else if (rows == Vector256<int>.Count)
            {
                Vector256<int> previous = Vector256.LoadUnsafe(ref neighbors);

                Vector256<int> current = Vector256.LoadUnsafe(ref averageStart);

                for (int column = 0; column < rows; column++)
                {
                    uint offset = (uint)(column * rows);

                    Vector256<int> next = column + 1 < rows
                        ? Vector256.LoadUnsafe(ref averageStart, offset + (uint)rows)
                        : Vector256.LoadUnsafe(ref neighbors, (uint)rows);

                    operation.Invoke(current, Vector256.LoadUnsafe(ref residualStart, offset), next, previous, out Vector256<int> even, out previous);
                    even.StoreUnsafe(ref averageStart, offset);
                    previous.StoreUnsafe(ref residualStart, offset);
                    current = next;
                }
            }
            else
            {
                Vector128<int> previous = Vector128.LoadUnsafe(ref neighbors);

                Vector128<int> current = Vector128.LoadUnsafe(ref averageStart);

                for (int column = 0; column < rows; column++)
                {
                    uint offset = (uint)(column * rows);

                    Vector128<int> next = column + 1 < rows
                        ? Vector128.LoadUnsafe(ref averageStart, offset + (uint)rows)
                        : Vector128.LoadUnsafe(ref neighbors, (uint)rows);

                    operation.Invoke(current, Vector128.LoadUnsafe(ref residualStart, offset), next, previous, out Vector128<int> even, out previous);
                    even.StoreUnsafe(ref averageStart, offset);
                    previous.StoreUnsafe(ref residualStart, offset);
                    current = next;
                }
            }

            SimdUtils.Transpose(averageTile, rows, rowTile, rows, rows, rows);

            SimdUtils.Transpose(residualTile, rows, averageTile, rows, rows, rows);

            // Both transposed buffers now contain image rows. Select the width before storing them; each store writes exactly two samples per pair.
            if (rows == Vector512<int>.Count)
            {
                for (int row = 0; row < rows; row++)
                {
                    ref int even = ref rowTile[row * rows];
                    ref int odd = ref averageTile[row * rows];
                    ref int destination = ref output[((firstRow + row) * outputStride) + (2 * x)];
                    SimdUtils.Interleave2(Vector512.LoadUnsafe(ref even), Vector512.LoadUnsafe(ref odd), ref destination);
                }
            }
            else if (rows == Vector256<int>.Count)
            {
                for (int row = 0; row < rows; row++)
                {
                    ref int even = ref rowTile[row * rows];
                    ref int odd = ref averageTile[row * rows];
                    ref int destination = ref output[((firstRow + row) * outputStride) + (2 * x)];
                    SimdUtils.Interleave2(Vector256.LoadUnsafe(ref even), Vector256.LoadUnsafe(ref odd), ref destination);
                }
            }
            else
            {
                for (int row = 0; row < rows; row++)
                {
                    ref int even = ref rowTile[row * rows];
                    ref int odd = ref averageTile[row * rows];
                    ref int destination = ref output[((firstRow + row) * outputStride) + (2 * x)];
                    SimdUtils.Interleave2(Vector128.LoadUnsafe(ref even), Vector128.LoadUnsafe(ref odd), ref destination);
                }
            }
        }

        for (int row = 0; row < rows; row++)
        {
            InverseHorizontalRow(
                averages.Slice((firstRow + row) * averageStride, averageWidth),
                residuals.Slice((firstRow + row) * residualStride, residualWidth),
                output.Slice((firstRow + row) * outputStride, outputWidth),
                x);
        }
    }

    /// <summary>
    /// Borrows a strided matrix column into a complete-register scratch span.
    /// </summary>
    /// <param name="source">The complete source allocation from its logical origin.</param>
    /// <param name="stride">The physical row stride in integers.</param>
    /// <param name="firstRow">The first source row.</param>
    /// <param name="column">The source column.</param>
    /// <param name="destination">Exactly four, eight or sixteen scratch samples, matching a supported register width.</param>
    private static void CopyColumn(ReadOnlySpan<int> source, int stride, int firstRow, int column, Span<int> destination)
    {
        ref int input = ref MemoryMarshal.GetReference(source);

        ref int output = ref MemoryMarshal.GetReference(destination);
        int offset = (firstRow * stride) + column;

        // Each index addresses one real sample. Plane allocation already bounds row offsets to Int32; the shared gather owns its pin scope.
        if (destination.Length == Vector512<int>.Count)
        {
            Vector512<int> offsets = (Vector512<int>.Indices * Vector512.Create(stride)) + Vector512.Create(offset);

            Vector512_.Gather(ref input, offsets).StoreUnsafe(ref output);
        }
        else if (destination.Length == Vector256<int>.Count)
        {
            Vector256<int> offsets = (Vector256<int>.Indices * Vector256.Create(stride)) + Vector256.Create(offset);

            Vector256_.Gather(ref input, offsets).StoreUnsafe(ref output);
        }
        else
        {
            Vector128<int> offsets = (Vector128<int>.Indices * Vector128.Create(stride)) + Vector128.Create(offset);

            Vector128_.Gather(ref input, offsets).StoreUnsafe(ref output);
        }
    }

    /// <summary>
    /// Restores the remaining pairs of one row with exact 64-bit intermediate arithmetic.
    /// </summary>
    /// <param name="averages">The logical average row.</param>
    /// <param name="residuals">The logical residual row.</param>
    /// <param name="output">The logical restored row.</param>
    /// <param name="firstPair">The first pair not already restored by tiled SIMD traversal.</param>
    private static void InverseHorizontalRow(ReadOnlySpan<int> averages, ReadOnlySpan<int> residuals, Span<int> output, int firstPair)
    {
        InverseSqueezeOperator operation = default;
        for (int x = firstPair; x < residuals.Length; x++)
        {
            int average = averages[x];
            int nextAverage = x + 1 < averages.Length ? averages[x + 1] : average;
            int previous = x > 0 ? output[(2 * x) - 1] : average;
            operation.Invoke(average, residuals[x], nextAverage, previous, out int even, out int odd);

            output[2 * x] = even;
            output[(2 * x) + 1] = odd;
        }

        if ((output.Length & 1) != 0)
        {
            output[^1] = averages[^1];
        }
    }

    /// <summary>
    /// Restores a vertical channel from its average and residual planes, then releases the displaced average storage.
    /// </summary>
    /// <param name="configuration">The configuration used for restored samples.</param>
    /// <param name="input">The owner of the average and residual channels.</param>
    /// <param name="c">The average channel index, which receives restored samples.</param>
    /// <param name="rc">The residual channel index. InverseSqueeze removes it after reconstruction.</param>
    public static void InverseVerticalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        DebugGuard.MustBeLessThan(c, input.Channels.Count, nameof(c));

        DebugGuard.MustBeLessThan(rc, input.Channels.Count, nameof(rc));
        JxlModularChannel average = input.Channels[c];
        JxlModularChannel residual = input.Channels[rc];
        if (average.Height != JxlMath.DivCeil(average.Height + residual.Height, 2))
        {
            throw new InvalidOperationException("Invalid height");
        }

        if (average.Width != residual.Width)
        {
            throw new InvalidOperationException("Width of the input channel must be equal to the width of the residual channel");
        }

        if (residual.Height == 0)
        {
            average.VerticalShift--;
            return;
        }

        using JxlModularChannel output = new(
            configuration,
            average.Width,
            average.Height + residual.Height,
            average.HorizontalShift,
            average.VerticalShift - 1);

        // Reconstruction changes channel resolution but retains the represented component.
        output.Component = average.Component;

        ReadOnlySpan<int> averages = average.GetRow(0);
        ReadOnlySpan<int> residuals = residual.GetRow(0);
        Span<int> restored = output.GetRow(0);

        int averageStride = average.Plane.PixelsPerRow;
        int residualStride = residual.Plane.PixelsPerRow;
        int outputStride = output.Plane.PixelsPerRow;
        int width = average.Width;
        InverseSqueezeOperator operation = default;

        // Columns are independent. Each vector contains adjacent columns of the same row; rows remain sequential for predecessor reconstruction.
        for (int y = 0; y < residual.Height; y++)
        {
            ReadOnlySpan<int> currentAverage = averages.Slice(y * averageStride, width);
            ReadOnlySpan<int> currentResidual = residuals.Slice(y * residualStride, width);
            int nextRow = y + 1 < average.Height ? y + 1 : y;
            ReadOnlySpan<int> nextAverage = averages.Slice(nextRow * averageStride, width);
            ReadOnlySpan<int> previous = y > 0 ? restored.Slice(((2 * y) - 1) * outputStride, width) : currentAverage;
            Span<int> evenRow = restored.Slice(2 * y * outputStride, width);
            Span<int> oddRow = restored.Slice(((2 * y) + 1) * outputStride, width);
            ref int a = ref MemoryMarshal.GetReference(currentAverage);

            ref int r = ref MemoryMarshal.GetReference(currentResidual);
            ref int n = ref MemoryMarshal.GetReference(nextAverage);

            ref int p = ref MemoryMarshal.GetReference(previous);
            ref int evenOutput = ref MemoryMarshal.GetReference(evenRow);

            ref int oddOutput = ref MemoryMarshal.GetReference(oddRow);
            int x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<int>.Count; x += Vector512<int>.Count)
                {
                    operation.Invoke(
                        Vector512.LoadUnsafe(ref a, (uint)x),
                        Vector512.LoadUnsafe(ref r, (uint)x),
                        Vector512.LoadUnsafe(ref n, (uint)x),
                        Vector512.LoadUnsafe(ref p, (uint)x),
                        out Vector512<int> even,
                        out Vector512<int> odd);

                    even.StoreUnsafe(ref evenOutput, (uint)x);
                    odd.StoreUnsafe(ref oddOutput, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<int>.Count; x += Vector256<int>.Count)
                {
                    operation.Invoke(
                        Vector256.LoadUnsafe(ref a, (uint)x),
                        Vector256.LoadUnsafe(ref r, (uint)x),
                        Vector256.LoadUnsafe(ref n, (uint)x),
                        Vector256.LoadUnsafe(ref p, (uint)x),
                        out Vector256<int> even,
                        out Vector256<int> odd);

                    even.StoreUnsafe(ref evenOutput, (uint)x);
                    odd.StoreUnsafe(ref oddOutput, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<int>.Count; x += Vector128<int>.Count)
                {
                    operation.Invoke(
                        Vector128.LoadUnsafe(ref a, (uint)x),
                        Vector128.LoadUnsafe(ref r, (uint)x),
                        Vector128.LoadUnsafe(ref n, (uint)x),
                        Vector128.LoadUnsafe(ref p, (uint)x),
                        out Vector128<int> even,
                        out Vector128<int> odd);

                    even.StoreUnsafe(ref evenOutput, (uint)x);
                    odd.StoreUnsafe(ref oddOutput, (uint)x);
                }
            }

            for (; x < width; x++)
            {
                operation.Invoke(currentAverage[x], currentResidual[x], nextAverage[x], previous[x], out int even, out int odd);

                evenRow[x] = even;
                oddRow[x] = odd;
            }
        }

        if ((output.Height & 1) != 0)
        {
            // An unmatched final average is already a complete source row, not another pair with a residual.
            averages.Slice((average.Height - 1) * averageStride, width).CopyTo(restored.Slice((output.Height - 1) * outputStride, width));
        }

        average.Swap(output);
    }

    /// <summary>
    /// Reconstructs squeezed channels in reverse transform order and releases each consumed residual channel.
    /// </summary>
    /// <param name="configuration">The configuration used for restored samples.</param>
    /// <param name="input">The owner of the transformed channels.</param>
    /// <param name="parameters">The squeeze steps in forward order.</param>
    public static void InverseSqueeze(Configuration configuration, JxlModularImage input, Span<JxlSqueezeParameters> parameters)
    {
        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            int totalNumberOfChannels = input.Channels.Count;
            ref JxlSqueezeParameters parameter = ref parameters[i];

            CheckMetaSqueezeParameters(parameter, totalNumberOfChannels);

            bool horizontal = parameter.Horizontal;
            bool inPlace = parameter.InPlace;
            int beginC = parameter.BeginC;
            int endC = parameter.BeginC + parameter.NumC - 1;

            int offset = inPlace
                ? endC + 1
                : totalNumberOfChannels + beginC - endC - 1;

            if (beginC < input.MetaChannels)
            {
                if (input.MetaChannels <= parameter.NumC)
                {
                    throw new InvalidOperationException("Not enough meta channels");
                }

                input.MetaChannels -= parameter.NumC;
            }

            for (int c = beginC; c <= endC; c++)
            {
                int rc = offset + c - beginC;

                if (rc >= totalNumberOfChannels)
                {
                    throw new InvalidOperationException("Residual channel offset out of bounds");
                }

                JxlModularChannel channelC = input.Channels[c]; // Input channel
                JxlModularChannel channelRC = input.Channels[rc]; // Residual channel

                if (channelC.Width < channelRC.Width || channelC.Height < channelRC.Height)
                {
                    throw new InvalidOperationException("Input channel width or height does not match residual channel width/height");
                }

                if (horizontal)
                {
                    InverseHorizontalSqueeze(configuration, input, c, rc);
                }
                else
                {
                    InverseVerticalSqueeze(configuration, input, c, rc);
                }
            }

            // Every step appends one residual per selected channel, either after that range or at the image's end.
            // Remove only this step's residuals; the next reverse step must see the reduced channel count.
            for (int c = offset; c < offset + parameter.NumC; c++)
            {
                input.Channels[c].Dispose();
            }

            input.Channels.RemoveRange(offset, parameter.NumC);
        }
    }

    /// <summary>
    /// Selects squeeze steps that reduce the image to its first preview dimensions.
    /// </summary>
    /// <param name="squeezeParameters">The list replaced with the selected steps.</param>
    /// <param name="image">The borrowed channel shapes used to select the steps.</param>
    public static void DefaultSqueezeParameters(List<JxlSqueezeParameters> squeezeParameters, JxlModularImage image)
    {
        int numberOfChannels = image.Channels.Count - image.MetaChannels;
        squeezeParameters.Clear();

        JxlModularChannel numMetaChannelsChannel = image.Channels[image.MetaChannels];
        int w = numMetaChannelsChannel.Width;
        int h = numMetaChannelsChannel.Height;
        bool wide = w > h;

        if (numberOfChannels > 2 && image.Channels[image.MetaChannels + 1].Width == w && image.Channels[image.MetaChannels + 1].Height == h)
        {
            JxlSqueezeParameters parameters = new()
            {
                Horizontal = true,
                InPlace = false,
                BeginC = image.MetaChannels + 1,
                NumC = 2
            };

            squeezeParameters.Add(parameters);

            parameters.Horizontal = false;
            squeezeParameters.Add(parameters);
        }

        JxlSqueezeParameters newParameters = new()
        {
            BeginC = image.MetaChannels,
            NumC = numberOfChannels,
            InPlace = true
        };

        if (!wide)
        {
            if (h > MaxFirstPreviewSize)
            {
                newParameters.Horizontal = false;
                squeezeParameters.Add(newParameters);

                h = (h + 1) >> 1;
            }
        }

        while (w > MaxFirstPreviewSize || h > MaxFirstPreviewSize)
        {
            if (w > MaxFirstPreviewSize)
            {
                newParameters.Horizontal = true;
                squeezeParameters.Add(newParameters);

                w = (w + 1) >> 1;
            }

            if (h > MaxFirstPreviewSize)
            {
                newParameters.Horizontal = false;
                squeezeParameters.Add(newParameters);

                h = (h + 1) >> 1;
            }
        }
    }

    /// <summary>
    /// Validates an encoded squeeze channel range against the current channel count.
    /// </summary>
    /// <param name="parameter">The encoded step.</param>
    /// <param name="numChannels">The channel count before applying that step.</param>
    private static void CheckMetaSqueezeParameters(in JxlSqueezeParameters parameter, int numChannels)
    {
        int c1 = parameter.BeginC;
        int c2 = parameter.BeginC + parameter.NumC - 1;

        if ((uint)c1 >= numChannels ||
            (uint)c2 >= numChannels ||
            c2 < c1)
        {
            throw new InvalidOperationException("Invalid channel range");
        }
    }

    /// <summary>
    /// Reduces channel shapes before decoding and attaches the corresponding owned residual planes.
    /// </summary>
    /// <param name="configuration">The configuration used for residual samples.</param>
    /// <param name="image">The image that owns the reduced and residual channels.</param>
    /// <param name="parameters">The encoded steps, or an empty list to receive the default steps.</param>
    public static void MetaSqueeze(Configuration configuration, JxlModularImage image, List<JxlSqueezeParameters> parameters)
    {
        if (parameters.Count == 0)
        {
            DefaultSqueezeParameters(parameters, image);
        }

        foreach (JxlSqueezeParameters parameter in parameters)
        {
            CheckMetaSqueezeParameters(parameter, image.Channels.Count);

            bool horizontal = parameter.Horizontal;
            bool inPlace = parameter.InPlace;
            int beginC = parameter.BeginC;
            int endC = parameter.BeginC + parameter.NumC - 1;

            if (beginC < image.MetaChannels)
            {
                if (endC >= image.MetaChannels)
                {
                    throw new InvalidOperationException("Invalid squeeze: mix of meta and nonmeta channels");
                }

                if (!inPlace)
                {
                    throw new InvalidOperationException("Invalid squeeze: meta channels require in-place residuals");
                }

                image.MetaChannels += parameter.NumC;
            }

            int offset = inPlace
                ? endC + 1
                : image.Channels.Count;

            // Reserve references before allocating residual samples. Each completed plane can then attach without list growth.
            image.Channels.EnsureCapacity(image.Channels.Count + parameter.NumC);

            for (int c = beginC; c <= endC; c++)
            {
                JxlModularChannel channel = image.Channels[c];

                if (channel.HorizontalShift > 30 || channel.VerticalShift > 30)
                {
                    throw new InvalidOperationException("Too many squeezes: shift > 30");
                }

                int w = channel.Width;
                int h = channel.Height;

                if (w == 0 || h == 0)
                {
                    throw new InvalidOperationException("Squeezing empty channel");
                }

                if (horizontal)
                {
                    channel.Width = (w + 1) >> 1;

                    if (channel.HorizontalShift >= 0)
                    {
                        channel.HorizontalShift++;
                    }

                    w -= (w + 1) >> 1;
                }
                else
                {
                    channel.Height = (h + 1) >> 1;

                    if (channel.VerticalShift >= 0)
                    {
                        channel.VerticalShift++;
                    }

                    h -= (h + 1) >> 1;
                }

                channel.Shrink();

                JxlModularChannel placeholder = new(configuration, w, h, channel.HorizontalShift, channel.VerticalShift)
                {
                    Component = channel.Component
                };

                image.Channels.Insert(offset + (c - beginC), placeholder);
            }
        }
    }

    /// <summary>
    /// Splits a horizontal channel into owned average and residual planes and releases its original samples.
    /// </summary>
    /// <param name="configuration">The configuration used for output samples.</param>
    /// <param name="input">The owner of input and output channels.</param>
    /// <param name="c">The channel to split.</param>
    /// <param name="rc">The insertion index for its residual channel.</param>
    public static void ForwardHorizontalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        JxlModularChannel source = input.Channels[c];
        input.Channels.EnsureCapacity(input.Channels.Count + 1);

        using JxlModularChannel average = new(configuration, JxlMath.DivCeil(source.Width, 2), source.Height, source.HorizontalShift + 1, source.VerticalShift);

        JxlModularChannel residual = new(configuration, source.Width - average.Width, source.Height, source.HorizontalShift + 1, source.VerticalShift);

        // Attach the second owner before processing. The image releases it if any later operation fails.
        input.Channels.Insert(rc, residual);

        average.Component = source.Component;
        residual.Component = source.Component;
        ReadOnlySpan<int> samples = source.GetRow(0);
        Span<int> averages = average.GetRow(0);
        Span<int> residuals = residual.GetRow(0);

        int sourceStride = source.Plane.PixelsPerRow;
        int averageStride = average.Plane.PixelsPerRow;
        int residualStride = residual.Plane.PixelsPerRow;

        for (int y = 0; y < source.Height; y++)
        {
            ForwardHorizontalRow(
                samples.Slice(y * sourceStride, source.Width),
                averages.Slice(y * averageStride, average.Width),
                residuals.Slice(y * residualStride, residual.Width));
        }

        source.Swap(average);
    }

    /// <summary>
    /// Splits a complete source row using independent original sample pairs.
    /// </summary>
    /// <param name="source">The logical source row.</param>
    /// <param name="averages">The logical average row.</param>
    /// <param name="residuals">The logical residual row, one sample shorter for an odd source width.</param>
    private static void ForwardHorizontalRow(ReadOnlySpan<int> source, Span<int> averages, Span<int> residuals)
    {
        ref int input = ref MemoryMarshal.GetReference(source);

        ref int averageOutput = ref MemoryMarshal.GetReference(averages);
        ref int residualOutput = ref MemoryMarshal.GetReference(residuals);

        ForwardSqueezeOperator operation = default;
        int x = 0;

        // Forward prediction reads original neighbors, so adjacent pairs are independent and require no transposed tile.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= residuals.Length - Vector512<int>.Count; x += Vector512<int>.Count)
            {
                SimdUtils.Deinterleave2(
                    Vector512.LoadUnsafe(ref input, (uint)(2 * x)),
                    Vector512.LoadUnsafe(ref input, (uint)((2 * x) + Vector512<int>.Count)),
                    out Vector512<int> even,
                    out Vector512<int> odd);

                Vector512<int> average = Average(even, odd);

                int nextPair = x + Vector512<int>.Count;
                int lastAverage = nextPair < residuals.Length ? Average(source[2 * nextPair], source[(2 * nextPair) + 1])
                    : (source.Length & 1) != 0 ? source[^1] : average.GetElement(Vector512<int>.Count - 1);

                // Shift the already calculated averages rather than reloading each following pair. Only the final lane needs a new average.
                Vector512<int> nextAverage = Vector512.ShuffleNative(average, Vector512.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 15));
                nextAverage = nextAverage.WithElement(Vector512<int>.Count - 1, lastAverage);
                Vector512<int> previous = Vector512.ShuffleNative(odd, Vector512.Create(0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14));
                previous = previous.WithElement(0, x > 0 ? source[(2 * x) - 1] : average.ToScalar());
                average.StoreUnsafe(ref averageOutput, (uint)x);

                operation.Invoke(even, odd, average, nextAverage, previous).StoreUnsafe(ref residualOutput, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= residuals.Length - Vector256<int>.Count; x += Vector256<int>.Count)
            {
                SimdUtils.Deinterleave2(
                    Vector256.LoadUnsafe(ref input, (uint)(2 * x)),
                    Vector256.LoadUnsafe(ref input, (uint)((2 * x) + Vector256<int>.Count)),
                    out Vector256<int> even,
                    out Vector256<int> odd);

                Vector256<int> average = Average(even, odd);

                int nextPair = x + Vector256<int>.Count;
                int lastAverage = nextPair < residuals.Length ? Average(source[2 * nextPair], source[(2 * nextPair) + 1])
                    : (source.Length & 1) != 0 ? source[^1] : average.GetElement(Vector256<int>.Count - 1);

                // Shift the already calculated averages rather than reloading each following pair. Only the final lane needs a new average.
                Vector256<int> nextAverage = Vector256.ShuffleNative(average, Vector256.Create(1, 2, 3, 4, 5, 6, 7, 7));
                nextAverage = nextAverage.WithElement(Vector256<int>.Count - 1, lastAverage);
                Vector256<int> previous = Vector256.ShuffleNative(odd, Vector256.Create(0, 0, 1, 2, 3, 4, 5, 6));
                previous = previous.WithElement(0, x > 0 ? source[(2 * x) - 1] : average.ToScalar());
                average.StoreUnsafe(ref averageOutput, (uint)x);

                operation.Invoke(even, odd, average, nextAverage, previous).StoreUnsafe(ref residualOutput, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= residuals.Length - Vector128<int>.Count; x += Vector128<int>.Count)
            {
                SimdUtils.Deinterleave2(
                    Vector128.LoadUnsafe(ref input, (uint)(2 * x)),
                    Vector128.LoadUnsafe(ref input, (uint)((2 * x) + Vector128<int>.Count)),
                    out Vector128<int> even,
                    out Vector128<int> odd);

                Vector128<int> average = Average(even, odd);

                int nextPair = x + Vector128<int>.Count;
                int lastAverage = nextPair < residuals.Length ? Average(source[2 * nextPair], source[(2 * nextPair) + 1])
                    : (source.Length & 1) != 0 ? source[^1] : average.GetElement(Vector128<int>.Count - 1);

                // Shift the already calculated averages rather than reloading each following pair. Only the final lane needs a new average.
                Vector128<int> nextAverage = Vector128.ShuffleNative(average, Vector128.Create(1, 2, 3, 3));
                nextAverage = nextAverage.WithElement(Vector128<int>.Count - 1, lastAverage);
                Vector128<int> previous = Vector128.ShuffleNative(odd, Vector128.Create(0, 0, 1, 2));
                previous = previous.WithElement(0, x > 0 ? source[(2 * x) - 1] : average.ToScalar());
                average.StoreUnsafe(ref averageOutput, (uint)x);

                operation.Invoke(even, odd, average, nextAverage, previous).StoreUnsafe(ref residualOutput, (uint)x);
            }
        }

        for (; x < residuals.Length; x++)
        {
            int even = source[2 * x];
            int odd = source[(2 * x) + 1];
            int average = Average(even, odd);

            int nextAverage = x + 1 < residuals.Length ? Average(source[(2 * x) + 2], source[(2 * x) + 3])
                : (source.Length & 1) != 0 ? source[^1] : average;

            int previous = x > 0 ? source[(2 * x) - 1] : average;
            averages[x] = average;
            residuals[x] = operation.Invoke(even, odd, average, nextAverage, previous);
        }

        if ((source.Length & 1) != 0)
        {
            averages[^1] = source[^1];
        }
    }

    /// <summary>
    /// Splits a vertical channel into owned average and residual planes and releases its original samples.
    /// </summary>
    /// <param name="configuration">The configuration used for output samples.</param>
    /// <param name="input">The owner of input and output channels.</param>
    /// <param name="c">The channel to split.</param>
    /// <param name="rc">The insertion index for its residual channel.</param>
    public static void ForwardVerticalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        JxlModularChannel source = input.Channels[c];
        input.Channels.EnsureCapacity(input.Channels.Count + 1);

        using JxlModularChannel average = new(configuration, source.Width, JxlMath.DivCeil(source.Height, 2), source.HorizontalShift, source.VerticalShift + 1);

        JxlModularChannel residual = new(configuration, source.Width, source.Height - average.Height, source.HorizontalShift, source.VerticalShift + 1);

        input.Channels.Insert(rc, residual);

        average.Component = source.Component;
        residual.Component = source.Component;
        ReadOnlySpan<int> samples = source.GetRow(0);
        Span<int> averages = average.GetRow(0);
        Span<int> residuals = residual.GetRow(0);

        int sourceStride = source.Plane.PixelsPerRow;
        int averageStride = average.Plane.PixelsPerRow;
        int residualStride = residual.Plane.PixelsPerRow;
        int width = source.Width;
        ForwardSqueezeOperator operation = default;

        // Only paired rows have residuals. The final unpaired row is copied below and must not enter this loop.
        for (int y = 0; y < residual.Height; y++)
        {
            ReadOnlySpan<int> evenRow = samples.Slice(2 * y * sourceStride, width);
            ReadOnlySpan<int> oddRow = samples.Slice(((2 * y) + 1) * sourceStride, width);
            ReadOnlySpan<int> previousRow = y > 0 ? samples.Slice(((2 * y) - 1) * sourceStride, width) : evenRow;
            ReadOnlySpan<int> nextEvenRow = evenRow;
            ReadOnlySpan<int> nextOddRow = oddRow;
            if (y + 1 < residual.Height)
            {
                nextEvenRow = samples.Slice(((2 * y) + 2) * sourceStride, width);

                nextOddRow = samples.Slice(((2 * y) + 3) * sourceStride, width);
            }
            else if ((source.Height & 1) != 0)
            {
                // Treating the same unpaired row as both samples leaves its average unchanged.
                nextEvenRow = samples.Slice((source.Height - 1) * sourceStride, width);

                nextOddRow = nextEvenRow;
            }

            Span<int> averageRow = averages.Slice(y * averageStride, width);
            Span<int> residualRow = residuals.Slice(y * residualStride, width);
            ref int a = ref MemoryMarshal.GetReference(evenRow);

            ref int b = ref MemoryMarshal.GetReference(oddRow);
            ref int nextA = ref MemoryMarshal.GetReference(nextEvenRow);

            ref int nextB = ref MemoryMarshal.GetReference(nextOddRow);
            ref int p = ref MemoryMarshal.GetReference(previousRow);

            ref int averageOutput = ref MemoryMarshal.GetReference(averageRow);
            ref int residualOutput = ref MemoryMarshal.GetReference(residualRow);

            int x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<int>.Count; x += Vector512<int>.Count)
                {
                    Vector512<int> even = Vector512.LoadUnsafe(ref a, (uint)x);

                    Vector512<int> odd = Vector512.LoadUnsafe(ref b, (uint)x);
                    Vector512<int> averageValue = Average(even, odd);

                    Vector512<int> nextAverage = Average(Vector512.LoadUnsafe(ref nextA, (uint)x), Vector512.LoadUnsafe(ref nextB, (uint)x));
                    Vector512<int> previous = y > 0 ? Vector512.LoadUnsafe(ref p, (uint)x) : averageValue;
                    averageValue.StoreUnsafe(ref averageOutput, (uint)x);

                    operation.Invoke(even, odd, averageValue, nextAverage, previous).StoreUnsafe(ref residualOutput, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<int>.Count; x += Vector256<int>.Count)
                {
                    Vector256<int> even = Vector256.LoadUnsafe(ref a, (uint)x);

                    Vector256<int> odd = Vector256.LoadUnsafe(ref b, (uint)x);
                    Vector256<int> averageValue = Average(even, odd);

                    Vector256<int> nextAverage = Average(Vector256.LoadUnsafe(ref nextA, (uint)x), Vector256.LoadUnsafe(ref nextB, (uint)x));
                    Vector256<int> previous = y > 0 ? Vector256.LoadUnsafe(ref p, (uint)x) : averageValue;
                    averageValue.StoreUnsafe(ref averageOutput, (uint)x);

                    operation.Invoke(even, odd, averageValue, nextAverage, previous).StoreUnsafe(ref residualOutput, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<int>.Count; x += Vector128<int>.Count)
                {
                    Vector128<int> even = Vector128.LoadUnsafe(ref a, (uint)x);

                    Vector128<int> odd = Vector128.LoadUnsafe(ref b, (uint)x);
                    Vector128<int> averageValue = Average(even, odd);

                    Vector128<int> nextAverage = Average(Vector128.LoadUnsafe(ref nextA, (uint)x), Vector128.LoadUnsafe(ref nextB, (uint)x));
                    Vector128<int> previous = y > 0 ? Vector128.LoadUnsafe(ref p, (uint)x) : averageValue;
                    averageValue.StoreUnsafe(ref averageOutput, (uint)x);

                    operation.Invoke(even, odd, averageValue, nextAverage, previous).StoreUnsafe(ref residualOutput, (uint)x);
                }
            }

            for (; x < width; x++)
            {
                int averageValue = Average(evenRow[x], oddRow[x]);

                int nextAverage = Average(nextEvenRow[x], nextOddRow[x]);
                int previous = y > 0 ? previousRow[x] : averageValue;
                averageRow[x] = averageValue;
                residualRow[x] = operation.Invoke(evenRow[x], oddRow[x], averageValue, nextAverage, previous);
            }
        }

        if ((source.Height & 1) != 0)
        {
            samples.Slice((source.Height - 1) * sourceStride, width).CopyTo(averages.Slice((average.Height - 1) * averageStride, width));
        }

        source.Swap(average);
    }

    public static void ForwardSqueeze(Configuration configuration, JxlModularImage input, List<JxlSqueezeParameters> parameters)
    {
        if (parameters.Count == 0)
        {
            DefaultSqueezeParameters(parameters, input);

            if (parameters.Count == 0)
            {
                // If there's nothing to do, don't squeeze.
                return;
            }
        }

        foreach (JxlSqueezeParameters parameter in parameters)
        {
            CheckMetaSqueezeParameters(parameter, input.Channels.Count);

            bool horizontal = parameter.Horizontal;
            bool inPlace = parameter.InPlace;
            int beginC = parameter.BeginC;
            int endC = parameter.BeginC + parameter.NumC - 1;

            int offset = inPlace
                ? endC + 1
                : input.Channels.Count;

            for (int c = beginC; c <= endC; c++)
            {
                if (horizontal)
                {
                    ForwardHorizontalSqueeze(configuration, input, c, offset + c - beginC);
                }
                else
                {
                    ForwardVerticalSqueeze(configuration, input, c, offset + c - beginC);
                }
            }
        }
    }
}

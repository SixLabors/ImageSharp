// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Restores modular palette channels from explicit entries, implicit colors and optional prediction deltas.
/// </summary>
internal static class JxlModularPalette
{
    /// <summary>
    /// The scratch length for one component's signed deltas, four-level cube and five-level cube.
    /// </summary>
    public const int ImplicitEntryCount = DeltaEntryCount + SmallCubeEntries + LargeCubeEntries;

    /// <summary>
    /// The number of color components defined by the implicit palette.
    /// </summary>
    private const int RgbComponents = 3;

    /// <summary>
    /// The number of signed entries obtained from the 72 base delta colors.
    /// </summary>
    private const int DeltaEntryCount = 143;

    /// <summary>
    /// The number of entries in the first implicit four-level RGB cube.
    /// </summary>
    private const int SmallCubeEntries = 64;

    /// <summary>
    /// The number of entries in the implicit five-level RGB cube.
    /// </summary>
    private const int LargeCubeEntries = 125;

    /// <summary>
    /// The base-two divisor used to center the smaller cube's values between the larger cube's levels.
    /// </summary>
    private const int SmallCubeCenterShift = 3;

    /// <summary>
    /// The component precision used by the unscaled delta constants.
    /// </summary>
    private const int DeltaPrecision = 8;

    /// <summary>
    /// The maximum precision used to scale the implicit palette.
    /// </summary>
    private const int MaximumImplicitPrecision = 24;

    /// <summary>
    /// The integer reciprocal numerator for exact division by 125 on nonnegative signed integers.
    /// </summary>
    private const int Divide125Multiplier = 1099511628;

    /// <summary>
    /// The reciprocal denominator's base-two exponent for division by 125.
    /// </summary>
    private const int Divide125Shift = 37;

    /// <summary>
    /// The integer reciprocal numerator for exact division by 143 on nonnegative signed integers.
    /// </summary>
    private const int Divide143Multiplier = 961111563;

    /// <summary>
    /// The reciprocal denominator's base-two exponent for division by 143.
    /// </summary>
    private const int Divide143Shift = 37;

    /// <summary>
    /// Gets the 72 base delta colors in interleaved RGB order. Constant data needs no managed table allocation.
    /// </summary>
    private static ReadOnlySpan<int> DeltaColors =>
    [
        0, 0, 0, 4, 4, 4, 11, 0, 0, 0, 0, -13, 0, -12, 0, -10, -10, -10,
        -18, -18, -18, -27, -27, -27, -18, -18, 0, 0, 0, -32, -32, 0, 0, -37, -37, -37,
        0, -32, -32, 24, 24, 45, 50, 50, 50, -45, -24, -24, -24, -45, -45, 0, -24, -24,
        -34, -34, 0, -24, 0, -24, -45, -45, -24, 64, 64, 64, -32, 0, -32, 0, -32, 0,
        -32, 0, 32, -24, -45, -24, 45, 24, 45, 24, -24, -45, -45, -24, 24, 80, 80, 80,
        64, 0, 0, 0, 0, -64, 0, -64, -64, -24, -24, 45, 96, 96, 96, 64, 64, 0,
        45, -24, -24, 34, -34, 0, 112, 112, 112, 24, -45, -45, 45, 45, -24, 0, -32, 32,
        24, -24, 45, 0, 96, 96, 45, -24, 24, 24, -45, -24, -24, -45, 24, 0, -64, 0,
        96, 0, 0, 128, 128, 128, 64, 0, 64, 144, 144, 144, 96, 96, 0, -36, -36, 36,
        45, -24, -45, 45, -45, -24, 0, 0, -96, 0, 128, 128, 0, 96, 0, 45, 24, -45,
        -128, 0, 0, 24, -45, 24, -45, 24, -45, 64, 0, -64, 64, -64, -64, 96, 0, 96,
        45, -45, 24, 24, 45, -45, 64, 64, -64, 128, 128, 0, 0, 0, -128, -24, 45, -45
    ];

    /// <summary>
    /// Restores equally sized output channels while preserving the index plane until the final output is reconstructed.
    /// </summary>
    /// <typeparam name="TSample">The signed modular storage type selected before traversal.</typeparam>
    /// <param name="indices">The decoded signed index plane. The first output can alias this region.</param>
    /// <param name="palette">The explicit palette, with one row per output component. A zero-width palette needs no backing storage.</param>
    /// <param name="outputs">The ready output regions in component order. The first can reuse the index region.</param>
    /// <param name="deltaCount">The number of explicit entries that contain prediction deltas.</param>
    /// <param name="predictor">The validated predictor code, from zero through thirteen.</param>
    /// <param name="bitDepth">The encoded sample precision used to scale implicit colors.</param>
    /// <param name="weightedPredictor">The group's weighted predictor coefficients.</param>
    /// <param name="scratch">Reusable weighted error storage large enough for an output row.</param>
    /// <param name="implicitScratch">At least ImplicitEntryCount integers, separate from the error rows and all channel samples.</param>
    /// <param name="predictionRows">Three reusable wide rows for signed 16-bit prediction; empty for 32-bit storage.</param>
    /// <param name="cancellationToken">The token checked between rows and output channels.</param>
    public static void Apply<TSample>(
        JxlModularChannel indices,
        JxlModularChannel palette,
        ReadOnlySpan<JxlModularChannel> outputs,
        int deltaCount,
        byte predictor,
        int bitDepth,
        JxlWeightedPredictor weightedPredictor,
        Span<int> scratch,
        Span<int> implicitScratch,
        Span<int> predictionRows,
        CancellationToken cancellationToken)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        // Empty transformed channels retain their logical dimensions but have no samples or row storage to borrow.
        if (indices.Width == 0 || indices.Height == 0)
        {
            return;
        }

        // Restore output zero last so it can reuse the index region. Local final transforms can instead supply
        // separate borrowed targets. Delta prediction remains independent because each output uses only its own restored neighbors.
        for (int i = 0; i < outputs.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int component = i == outputs.Length - 1 ? 0 : i + 1;
            JxlModularChannel output = outputs[component];
            ReadOnlySpan<int> table = [];
            if (palette.Width != 0)
            {
                ReadOnlySpan<TSample> stored = palette.GetRow<TSample>(component);
                if (typeof(TSample) == typeof(short))
                {
                    // Gather reads integer entries. Widen only this small explicit table component;
                    // image samples remain in their selected storage and no full-size shadow plane exists.
                    Span<int> widened = implicitScratch.Slice(ImplicitEntryCount, palette.Width);
                    TensorPrimitives.ConvertTruncating(stored, widened);
                    table = widened;
                }
                else
                {
                    table = MemoryMarshal.Cast<TSample, int>(stored);
                }
            }

            // The caller borrows this table once after entropy decoding. Refill it for each component only after
            // the preceding component is complete; no row or prediction state retains the old table values.
            PaletteLookupOperator lookup = new(table, implicitScratch[..ImplicitEntryCount], component, bitDepth);

            if (predictor == 0)
            {
                for (int y = 0; y < output.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyLookup<TSample>(indices.GetRow<TSample>(y), output.GetRow<TSample>(y), in lookup);
                }
            }
            else if (predictor == 6)
            {
                // Weighted state advances for every sample, including absolute palette entries.
                // Reset the same caller-owned storage for each independent output channel.
                weightedPredictor.Reset(scratch, output.Width);
                ApplyWeighted<TSample>(indices, output, deltaCount, weightedPredictor, scratch, predictionRows, in lookup, cancellationToken);
            }
            else
            {
                ApplyPredicted<TSample>(indices, output, deltaCount, predictor, predictionRows, in lookup, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Expands independent index values with descending SIMD widths and a scalar remainder.
    /// </summary>
    /// <typeparam name="TSample">The signed modular storage type selected before traversal.</typeparam>
    /// <param name="indices">The input index row, which can alias the destination.</param>
    /// <param name="destination">The output component row.</param>
    /// <param name="lookup">The component's prepared palette arithmetic.</param>
    private static void ApplyLookup<TSample>(ReadOnlySpan<TSample> indices, Span<TSample> destination, in PaletteLookupOperator lookup)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        int x = 0;

        // Each lane is one independent column. Load its index before the store so output zero can reuse this row in place.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = indices.Length.Vector512Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector512<int>.Count)
            {
                Vector512<int> values = lookup.Invoke(JxlModularChannel.Load512(indices, x));
                JxlModularChannel.Store512(destination, x, values);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (indices.Length - x).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
            {
                Vector256<int> values = lookup.Invoke(JxlModularChannel.Load256(indices, x));
                JxlModularChannel.Store256(destination, x, values);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (indices.Length - x).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
            {
                Vector128<int> values = lookup.Invoke(JxlModularChannel.Load128(indices, x));
                JxlModularChannel.Store128(destination, x, values);
            }
        }

        // Scalar lookup uses the same explicit range, negative cycle and implicit cube equations.
        for (; x < indices.Length; x++)
        {
            destination[x] = TSample.CreateTruncating(lookup.Invoke(int.CreateTruncating(indices[x])));
        }
    }

    /// <summary>
    /// Restores delta entries with an existing nonweighted modular predictor.
    /// </summary>
    /// <typeparam name="TSample">The signed modular storage type selected before traversal.</typeparam>
    /// <param name="indices">The unchanged input indices, or the region being restored in place.</param>
    /// <param name="output">The current component's output region.</param>
    /// <param name="deltaCount">The first index that denotes an absolute entry.</param>
    /// <param name="predictor">The validated nonzero, nonweighted predictor.</param>
    /// <param name="predictionRows">Three reusable wide rows for signed 16-bit prediction; empty for 32-bit storage.</param>
    /// <param name="lookup">The component's palette arithmetic.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void ApplyPredicted<TSample>(
        JxlModularChannel indices,
        JxlModularChannel output,
        int deltaCount,
        byte predictor,
        Span<int> predictionRows,
        in PaletteLookupOperator lookup,
        CancellationToken cancellationToken)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        int rowLength = output.Width;

        for (int y = 0; y < output.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadOnlySpan<TSample> indexRow = indices.GetRow<TSample>(y);
            Span<int> row = output.GetPredictionRow<TSample>(y, predictionRows);
            ReadOnlySpan<int> top = y == 0 ? row : output.GetPredictionRow<TSample>(y - 1, predictionRows);
            ReadOnlySpan<int> above = y < 2 ? top : output.GetPredictionRow<TSample>(y - 2, predictionRows);
            ref int first = ref MemoryMarshal.GetReference(row);
            int x = 0;

            // Only indices below deltaCount add a prediction. A block of absolute entries has no
            // dependency between columns, so the existing lookup operator can restore it with SIMD.
            // A mixed block retains scalar column order. Later deltas can then read every preceding
            // absolute or delta result, including values written by a preceding SIMD block.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<int> deltaLimit = Vector512.Create(deltaCount);
                nuint count = (rowLength - x).Vector512Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector512<int>.Count)
                {
                    Vector512<int> values = JxlModularChannel.Load512(indexRow, x);
                    if (!Vector512.LessThanAny(values, deltaLimit))
                    {
                        lookup.Invoke(values).StoreUnsafe(ref first, (nuint)x);
                    }
                    else
                    {
                        ApplyPredictedBlock(indexRow, row, top, above, x, Vector512<int>.Count, y, deltaCount, predictor, in lookup);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> deltaLimit = Vector256.Create(deltaCount);
                nuint count = (rowLength - x).Vector256Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
                {
                    Vector256<int> values = JxlModularChannel.Load256(indexRow, x);
                    if (!Vector256.LessThanAny(values, deltaLimit))
                    {
                        lookup.Invoke(values).StoreUnsafe(ref first, (nuint)x);
                    }
                    else
                    {
                        ApplyPredictedBlock(indexRow, row, top, above, x, Vector256<int>.Count, y, deltaCount, predictor, in lookup);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> deltaLimit = Vector128.Create(deltaCount);
                nuint count = (rowLength - x).Vector128Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
                {
                    Vector128<int> values = JxlModularChannel.Load128(indexRow, x);
                    if (!Vector128.LessThanAny(values, deltaLimit))
                    {
                        lookup.Invoke(values).StoreUnsafe(ref first, (nuint)x);
                    }
                    else
                    {
                        ApplyPredictedBlock(indexRow, row, top, above, x, Vector128<int>.Count, y, deltaCount, predictor, in lookup);
                    }
                }
            }

            // Load each index before its store. This also permits the final component to replace
            // the original index row in place, for both scalar blocks and the final short remainder.
            ApplyPredictedBlock(indexRow, row, top, above, x, rowLength - x, y, deltaCount, predictor, in lookup);
            output.CommitPredictionRow<TSample>(y, row);
        }
    }

    /// <summary>
    /// Restores a dependent block in column order using the same neighbors as scalar palette decoding.
    /// </summary>
    /// <typeparam name="TSample">The signed modular storage type selected before traversal.</typeparam>
    /// <param name="indices">The complete index row, which can alias the output row.</param>
    /// <param name="row">The complete current output row. Columns before start are already restored.</param>
    /// <param name="top">The preceding output row, or the current row at the top image edge.</param>
    /// <param name="above">The output row two rows above, or top at the first two image rows.</param>
    /// <param name="start">The first column in this block.</param>
    /// <param name="length">The number of columns in this block.</param>
    /// <param name="y">The output row's zero-based image coordinate.</param>
    /// <param name="deltaCount">The first index that denotes an absolute entry.</param>
    /// <param name="predictor">The validated nonzero, nonweighted predictor.</param>
    /// <param name="lookup">The component's prepared palette arithmetic.</param>
    private static void ApplyPredictedBlock<TSample>(
        ReadOnlySpan<TSample> indices,
        Span<int> row,
        ReadOnlySpan<int> top,
        ReadOnlySpan<int> above,
        int start,
        int length,
        int y,
        int deltaCount,
        byte predictor,
        in PaletteLookupOperator lookup)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample indexStart = ref MemoryMarshal.GetReference(indices);
        ref int first = ref MemoryMarshal.GetReference(row);
        ref int topStart = ref MemoryMarshal.GetReference(top);
        ref int aboveStart = ref MemoryMarshal.GetReference(above);
        int end = start + length;
        for (int x = start; x < end; x++)
        {
            int index = int.CreateTruncating(Unsafe.Add(ref indexStart, x));
            int value = lookup.Invoke(index);
            if (index < deltaCount)
            {
                // Negative implicit indices are deltas too. Predict from already restored output,
                // never from the index plane or from another output channel. Keep image coordinates
                // here: block edges are not image edges, and they do not replace valid neighbors.
                int left = x != 0 ? Unsafe.Add(ref first, x - 1) : y != 0 ? topStart : 0;
                int north = y != 0 ? Unsafe.Add(ref topStart, x) : left;
                int topLeft = x != 0 && y != 0 ? Unsafe.Add(ref topStart, x - 1) : left;
                int topRight = y != 0 && x + 1 < row.Length ? Unsafe.Add(ref topStart, x + 1) : north;
                int leftLeft = x > 1 ? Unsafe.Add(ref first, x - 2) : left;
                int topTop = y > 1 ? Unsafe.Add(ref aboveStart, x) : north;
                int topRightRight = y != 0 && x + 2 < row.Length ? Unsafe.Add(ref topStart, x + 2) : topRight;
                long prediction = JxlModularPredictor.Predict(predictor, left, north, topLeft, topRight, leftLeft, topTop, topRightRight);
                value = unchecked((int)(prediction + value));
            }

            Unsafe.Add(ref first, x) = value;
        }
    }

    /// <summary>
    /// Restores delta entries while advancing the weighted predictor for both absolute and delta samples.
    /// </summary>
    /// <typeparam name="TSample">The signed modular storage type selected before traversal.</typeparam>
    /// <param name="indices">The unchanged input indices, or the region being restored in place.</param>
    /// <param name="output">The current component's output region.</param>
    /// <param name="deltaCount">The first index that denotes an absolute entry.</param>
    /// <param name="weightedPredictor">The reset weighted predictor for this output channel.</param>
    /// <param name="scratch">The caller-owned error rows reused throughout this channel.</param>
    /// <param name="predictionRows">Three reusable wide rows for signed 16-bit prediction; empty for 32-bit storage.</param>
    /// <param name="lookup">The component's palette arithmetic.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void ApplyWeighted<TSample>(
        JxlModularChannel indices,
        JxlModularChannel output,
        int deltaCount,
        JxlWeightedPredictor weightedPredictor,
        Span<int> scratch,
        Span<int> predictionRows,
        in PaletteLookupOperator lookup,
        CancellationToken cancellationToken)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        int rowLength = output.Width;

        for (int y = 0; y < output.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The validated regions have equal widths. Borrow their rows once and keep the same edge
            // substitutions as nonweighted prediction. The index load precedes its possible in-place output store.
            ref TSample indexStart = ref MemoryMarshal.GetReference(indices.GetRow<TSample>(y));
            ref int first = ref MemoryMarshal.GetReference(output.GetPredictionRow<TSample>(y, predictionRows));
            ref int topStart = ref (y == 0 ? ref first : ref MemoryMarshal.GetReference(output.GetPredictionRow<TSample>(y - 1, predictionRows)));
            ref int aboveStart = ref (y < 2 ? ref topStart : ref MemoryMarshal.GetReference(output.GetPredictionRow<TSample>(y - 2, predictionRows)));

            for (int x = 0; x < rowLength; x++)
            {
                int index = int.CreateTruncating(Unsafe.Add(ref indexStart, x));
                int value = lookup.Invoke(index);
                int left = x != 0 ? Unsafe.Add(ref first, x - 1) : y != 0 ? topStart : 0;
                int top = y != 0 ? Unsafe.Add(ref topStart, x) : left;
                int topLeft = x != 0 && y != 0 ? Unsafe.Add(ref topStart, x - 1) : left;
                int topRight = y != 0 && x + 1 < rowLength ? Unsafe.Add(ref topStart, x + 1) : top;
                int topTop = y > 1 ? Unsafe.Add(ref aboveStart, x) : top;
                long prediction = weightedPredictor.Predict(scratch, x, y, left, top, topLeft, topRight, topTop, out _);
                if (index < deltaCount)
                {
                    value = unchecked((int)(prediction + value));
                }

                Unsafe.Add(ref first, x) = value;
                weightedPredictor.Update(scratch, x, y, value);
            }

            output.CommitPredictionRow<TSample>(y, MemoryMarshal.CreateReadOnlySpan(ref first, rowLength));
        }
    }

    /// <summary>
    /// Divides 4 nonnegative signed integers by a constant using exact widened products.
    /// </summary>
    /// <param name="values">Values from zero through int.MaxValue.</param>
    /// <param name="multiplier">The reciprocal numerator for one of the two palette divisors.</param>
    /// <param name="shift">The reciprocal denominator's base-two exponent.</param>
    /// <returns>The integer quotient in each original lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> DividePositive(Vector128<int> values, int multiplier, int shift)
    {
        // For divisor d and m=ceil(2^shift/d), (m*d-2^shift)*int.MaxValue < 2^shift proves exact truncation.
        // For 125 the excess numerator is 28, and for 143 it is 37. Both satisfy the bound without a correction step.
        Vector128<int> factor = Vector128.Create(multiplier);
        Vector128<long> even = Vector128_.MultiplyWideningEven(values, factor) >> shift;

        // Each 64-bit lane holds two input integers. Move its odd integer into the low half before widening,
        // then combine the quotient halves: [q0,0,q2,0] | [0,q1,0,q3], repeated within the wider vectors.
        Vector128<int> oddValues = (values.AsUInt64() >> 32).AsInt32();
        Vector128<long> odd = Vector128_.MultiplyWideningEven(oddValues, factor) >> shift;
        return (even | (odd << 32)).AsInt32();
    }

    /// <summary>
    /// Divides 8 nonnegative signed integers by a constant using exact widened products.
    /// </summary>
    /// <param name="values">Values from zero through int.MaxValue.</param>
    /// <param name="multiplier">The reciprocal numerator for one of the two palette divisors.</param>
    /// <param name="shift">The reciprocal denominator's base-two exponent.</param>
    /// <returns>The integer quotient in each original lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> DividePositive(Vector256<int> values, int multiplier, int shift)
    {
        // For divisor d and m=ceil(2^shift/d), (m*d-2^shift)*int.MaxValue < 2^shift proves exact truncation.
        // For 125 the excess numerator is 28, and for 143 it is 37. Both satisfy the bound without a correction step.
        Vector256<int> factor = Vector256.Create(multiplier);
        Vector256<long> even = Vector256_.MultiplyWideningEven(values, factor) >> shift;

        // Each 64-bit lane holds two input integers. Move its odd integer into the low half before widening,
        // then combine the quotient halves: [q0,0,q2,0] | [0,q1,0,q3], repeated within the wider vectors.
        Vector256<int> oddValues = (values.AsUInt64() >> 32).AsInt32();
        Vector256<long> odd = Vector256_.MultiplyWideningEven(oddValues, factor) >> shift;
        return (even | (odd << 32)).AsInt32();
    }

    /// <summary>
    /// Divides 16 nonnegative signed integers by a constant using exact widened products.
    /// </summary>
    /// <param name="values">Values from zero through int.MaxValue.</param>
    /// <param name="multiplier">The reciprocal numerator for one of the two palette divisors.</param>
    /// <param name="shift">The reciprocal denominator's base-two exponent.</param>
    /// <returns>The integer quotient in each original lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> DividePositive(Vector512<int> values, int multiplier, int shift)
    {
        // For divisor d and m=ceil(2^shift/d), (m*d-2^shift)*int.MaxValue < 2^shift proves exact truncation.
        // For 125 the excess numerator is 28, and for 143 it is 37. Both satisfy the bound without a correction step.
        Vector512<int> factor = Vector512.Create(multiplier);
        Vector512<long> even = Vector512_.MultiplyWideningEven(values, factor) >> shift;

        // Each 64-bit lane holds two input integers. Move its odd integer into the low half before widening,
        // then combine the quotient halves: [q0,0,q2,0] | [0,q1,0,q3], repeated within the wider vectors.
        Vector512<int> oddValues = (values.AsUInt64() >> 32).AsInt32();
        Vector512<long> odd = Vector512_.MultiplyWideningEven(oddValues, factor) >> shift;
        return (even | (odd << 32)).AsInt32();
    }

    /// <summary>
    /// Expands one output component using exact integer palette equations at every SIMD width.
    /// </summary>
    private readonly ref struct PaletteLookupOperator
    {
        private readonly ReadOnlySpan<int> palette;
        private readonly ReadOnlySpan<int> implicitValues;
        private readonly int component;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteLookupOperator"/> struct for one output component.
        /// </summary>
        /// <param name="palette">The explicit entries for this component, or an empty span.</param>
        /// <param name="implicitScratch">The caller's reusable component table, with at least ImplicitEntryCount entries.</param>
        /// <param name="component">The component's zero-based position within this palette transform.</param>
        /// <param name="bitDepth">The encoded sample precision.</param>
        public PaletteLookupOperator(ReadOnlySpan<int> palette, Span<int> implicitScratch, int component, int bitDepth)
        {
            this.palette = palette;
            this.implicitValues = implicitScratch;
            this.component = component;

            // Extra components, including alpha, have only explicit entries. Their lookup exits before reading this table.
            if (component >= RgbComponents)
            {
                return;
            }

            // Implicit values use at most 24 bits. Explicit entries retain their decoded signed integer precision.
            // Cube products are at most 4*(2^24-1). Scaled delta magnitudes are at most 144*2^16. Both fit in int.
            int precision = Math.Min(bitDepth, MaximumImplicitPrecision);
            int range = (1 << precision) - 1;
            int deltaShift = Math.Max(precision - DeltaPrecision, 0);
            int smallCubeOffset = 1 << Math.Max(precision - SmallCubeCenterShift, 0);

            // Layout: [143 signed deltas | 64 four-level cube entries | 125 five-level cube entries].
            // Scale each value once. Every scalar and SIMD lookup reads this same component table.
            ReadOnlySpan<int> deltas = DeltaColors;
            for (int i = 0; i < DeltaEntryCount; i++)
            {
                int value = deltas[(((i + 1) >> 1) * RgbComponents) + component];
                implicitScratch[i] = (value * (((i & 1) << 1) - 1)) << deltaShift;
            }

            for (int i = 0; i < SmallCubeEntries; i++)
            {
                int digit = (i >> (2 * component)) & 3;
                implicitScratch[DeltaEntryCount + i] = ((digit * range) >> 2) + smallCubeOffset;
            }

            int divisor = component == 0 ? 1 : component == 1 ? 5 : 25;
            for (int i = 0; i < LargeCubeEntries; i++)
            {
                implicitScratch[DeltaEntryCount + SmallCubeEntries + i] = (((i / divisor) % 5) * range) >> 2;
            }
        }

        /// <summary>
        /// Looks up one signed index without clipping implicit colors or prediction deltas.
        /// </summary>
        /// <param name="index">The signed decoded index.</param>
        /// <returns>The explicit value, implicit cube component or scaled implicit delta.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(int index)
        {
            if ((uint)index < (uint)this.palette.Length)
            {
                return this.palette[index];
            }

            // Extra components have no implicit colors. This includes alpha: implicit entries do not supply opaque alpha.
            if (this.component >= RgbComponents)
            {
                return 0;
            }

            if (index < 0)
            {
                // Complementing avoids negating int.MinValue. The prepared 143-entry cycle already includes signs and scaling.
                return this.implicitValues[~index % DeltaEntryCount];
            }

            int cube = index - this.palette.Length;
            if (cube < SmallCubeEntries)
            {
                return this.implicitValues[DeltaEntryCount + cube];
            }

            // The three component periods divide 125. Reducing the complete cube index retains each component's original digit.
            return this.implicitValues[DeltaEntryCount + SmallCubeEntries + ((cube - SmallCubeEntries) % LargeCubeEntries)];
        }

        /// <summary>
        /// Looks up 4 independent indices with one signed integer lane per column.
        /// </summary>
        /// <param name="indices">The signed decoded indices.</param>
        /// <returns>The exact scalar lookup result in each original lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<int> indices)
        {
            Vector128<int> explicitMask = Vector128<int>.Zero;
            Vector128<int> explicitValues = Vector128<int>.Zero;
            if (!this.palette.IsEmpty)
            {
                // Mask invalid lanes before gathering. Unselected lanes still perform reads, so their safe index must be inside the table.
                explicitMask = Vector128.GreaterThanOrEqual(indices, Vector128<int>.Zero)
                    & Vector128.LessThan(indices, Vector128.Create(this.palette.Length));
                Vector128<int> safeIndices = Vector128.ConditionalSelect(explicitMask, indices, Vector128<int>.Zero);
                explicitValues = Vector128_.Gather(ref MemoryMarshal.GetReference(this.palette), safeIndices);
                if (Vector128.EqualsAll(explicitMask, Vector128.Create(-1)))
                {
                    return explicitValues;
                }
            }

            if (this.component >= RgbComponents)
            {
                return Vector128.ConditionalSelect(explicitMask, explicitValues, Vector128<int>.Zero);
            }

            // Each lane maps one signed index to the prepared table. Complementing int.MinValue gives int.MaxValue,
            // so both reciprocal inputs stay in the proven nonnegative signed range.
            Vector128<int> negative = Vector128.Max(~indices, Vector128<int>.Zero);
            Vector128<int> delta = negative - (DividePositive(negative, Divide143Multiplier, Divide143Shift) * Vector128.Create(DeltaEntryCount));

            // Clamp only unused index arithmetic. Subtract after removing negative inputs so int.MinValue cannot wrap.
            // Masking the small-cube candidate keeps even unselected lanes inside its 64-entry portion of the table.
            Vector128<int> nonnegative = Vector128.Max(indices, Vector128<int>.Zero);
            Vector128<int> cube = Vector128.Max(nonnegative - Vector128.Create(this.palette.Length), Vector128<int>.Zero);
            Vector128<int> smallIndices = Vector128.Create(DeltaEntryCount) + (cube & Vector128.Create(SmallCubeEntries - 1));
            Vector128<int> large = Vector128.Max(cube - Vector128.Create(SmallCubeEntries), Vector128<int>.Zero);
            Vector128<int> largeRemainder = large - (DividePositive(large, Divide125Multiplier, Divide125Shift) * Vector128.Create(LargeCubeEntries));
            Vector128<int> largeIndices = Vector128.Create(DeltaEntryCount + SmallCubeEntries) + largeRemainder;
            Vector128<int> implicitIndices = Vector128.ConditionalSelect(
                Vector128.LessThan(cube, Vector128.Create(SmallCubeEntries)), smallIndices, largeIndices);

            // Select the bounded table index before gathering. Unselected signed-index families never perform an invalid read.
            implicitIndices = Vector128.ConditionalSelect(Vector128.LessThan(indices, Vector128<int>.Zero), delta, implicitIndices);
            Vector128<int> implicitValues = Vector128_.Gather(ref MemoryMarshal.GetReference(this.implicitValues), implicitIndices);
            return Vector128.ConditionalSelect(explicitMask, explicitValues, implicitValues);
        }

        /// <summary>
        /// Looks up 8 independent indices with one signed integer lane per column.
        /// </summary>
        /// <param name="indices">The signed decoded indices.</param>
        /// <returns>The exact scalar lookup result in each original lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<int> indices)
        {
            Vector256<int> explicitMask = Vector256<int>.Zero;
            Vector256<int> explicitValues = Vector256<int>.Zero;
            if (!this.palette.IsEmpty)
            {
                // Mask invalid lanes before gathering. Unselected lanes still perform reads, so their safe index must be inside the table.
                explicitMask = Vector256.GreaterThanOrEqual(indices, Vector256<int>.Zero)
                    & Vector256.LessThan(indices, Vector256.Create(this.palette.Length));
                Vector256<int> safeIndices = Vector256.ConditionalSelect(explicitMask, indices, Vector256<int>.Zero);
                explicitValues = Vector256_.Gather(ref MemoryMarshal.GetReference(this.palette), safeIndices);
                if (Vector256.EqualsAll(explicitMask, Vector256.Create(-1)))
                {
                    return explicitValues;
                }
            }

            if (this.component >= RgbComponents)
            {
                return Vector256.ConditionalSelect(explicitMask, explicitValues, Vector256<int>.Zero);
            }

            // Each lane maps one signed index to the prepared table. Complementing int.MinValue gives int.MaxValue,
            // so both reciprocal inputs stay in the proven nonnegative signed range.
            Vector256<int> negative = Vector256.Max(~indices, Vector256<int>.Zero);
            Vector256<int> delta = negative - (DividePositive(negative, Divide143Multiplier, Divide143Shift) * Vector256.Create(DeltaEntryCount));

            // Clamp only unused index arithmetic. Subtract after removing negative inputs so int.MinValue cannot wrap.
            // Masking the small-cube candidate keeps even unselected lanes inside its 64-entry portion of the table.
            Vector256<int> nonnegative = Vector256.Max(indices, Vector256<int>.Zero);
            Vector256<int> cube = Vector256.Max(nonnegative - Vector256.Create(this.palette.Length), Vector256<int>.Zero);
            Vector256<int> smallIndices = Vector256.Create(DeltaEntryCount) + (cube & Vector256.Create(SmallCubeEntries - 1));
            Vector256<int> large = Vector256.Max(cube - Vector256.Create(SmallCubeEntries), Vector256<int>.Zero);
            Vector256<int> largeRemainder = large - (DividePositive(large, Divide125Multiplier, Divide125Shift) * Vector256.Create(LargeCubeEntries));
            Vector256<int> largeIndices = Vector256.Create(DeltaEntryCount + SmallCubeEntries) + largeRemainder;
            Vector256<int> implicitIndices = Vector256.ConditionalSelect(
                Vector256.LessThan(cube, Vector256.Create(SmallCubeEntries)), smallIndices, largeIndices);

            // Select the bounded table index before gathering. Unselected signed-index families never perform an invalid read.
            implicitIndices = Vector256.ConditionalSelect(Vector256.LessThan(indices, Vector256<int>.Zero), delta, implicitIndices);
            Vector256<int> implicitValues = Vector256_.Gather(ref MemoryMarshal.GetReference(this.implicitValues), implicitIndices);
            return Vector256.ConditionalSelect(explicitMask, explicitValues, implicitValues);
        }

        /// <summary>
        /// Looks up 16 independent indices with one signed integer lane per column.
        /// </summary>
        /// <param name="indices">The signed decoded indices.</param>
        /// <returns>The exact scalar lookup result in each original lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<int> indices)
        {
            Vector512<int> explicitMask = Vector512<int>.Zero;
            Vector512<int> explicitValues = Vector512<int>.Zero;
            if (!this.palette.IsEmpty)
            {
                // Mask invalid lanes before gathering. Unselected lanes still perform reads, so their safe index must be inside the table.
                explicitMask = Vector512.GreaterThanOrEqual(indices, Vector512<int>.Zero)
                    & Vector512.LessThan(indices, Vector512.Create(this.palette.Length));
                Vector512<int> safeIndices = Vector512.ConditionalSelect(explicitMask, indices, Vector512<int>.Zero);
                explicitValues = Vector512_.Gather(ref MemoryMarshal.GetReference(this.palette), safeIndices);
                if (Vector512.EqualsAll(explicitMask, Vector512.Create(-1)))
                {
                    return explicitValues;
                }
            }

            if (this.component >= RgbComponents)
            {
                return Vector512.ConditionalSelect(explicitMask, explicitValues, Vector512<int>.Zero);
            }

            // Each lane maps one signed index to the prepared table. Complementing int.MinValue gives int.MaxValue,
            // so both reciprocal inputs stay in the proven nonnegative signed range.
            Vector512<int> negative = Vector512.Max(~indices, Vector512<int>.Zero);
            Vector512<int> delta = negative - (DividePositive(negative, Divide143Multiplier, Divide143Shift) * Vector512.Create(DeltaEntryCount));

            // Clamp only unused index arithmetic. Subtract after removing negative inputs so int.MinValue cannot wrap.
            // Masking the small-cube candidate keeps even unselected lanes inside its 64-entry portion of the table.
            Vector512<int> nonnegative = Vector512.Max(indices, Vector512<int>.Zero);
            Vector512<int> cube = Vector512.Max(nonnegative - Vector512.Create(this.palette.Length), Vector512<int>.Zero);
            Vector512<int> smallIndices = Vector512.Create(DeltaEntryCount) + (cube & Vector512.Create(SmallCubeEntries - 1));
            Vector512<int> large = Vector512.Max(cube - Vector512.Create(SmallCubeEntries), Vector512<int>.Zero);
            Vector512<int> largeRemainder = large - (DividePositive(large, Divide125Multiplier, Divide125Shift) * Vector512.Create(LargeCubeEntries));
            Vector512<int> largeIndices = Vector512.Create(DeltaEntryCount + SmallCubeEntries) + largeRemainder;
            Vector512<int> implicitIndices = Vector512.ConditionalSelect(
                Vector512.LessThan(cube, Vector512.Create(SmallCubeEntries)), smallIndices, largeIndices);

            // Select the bounded table index before gathering. Unselected signed-index families never perform an invalid read.
            implicitIndices = Vector512.ConditionalSelect(Vector512.LessThan(indices, Vector512<int>.Zero), delta, implicitIndices);
            Vector512<int> implicitValues = Vector512_.Gather(ref MemoryMarshal.GetReference(this.implicitValues), implicitIndices);
            return Vector512.ConditionalSelect(explicitMask, explicitValues, implicitValues);
        }
    }
}

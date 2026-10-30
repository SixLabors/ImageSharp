// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Restores modular palette channels from explicit entries, implicit colors and optional prediction deltas.
/// </summary>
internal static class JxlModularPalette
{
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
    /// The integer reciprocal and product shift for exact division by five on nonnegative signed integers.
    /// </summary>
    private const int Divide5Multiplier = 1717986919;
    private const int Divide5Shift = 33;

    /// <summary>
    /// The integer reciprocal and product shift for exact division by twenty-five on nonnegative signed integers.
    /// </summary>
    private const int Divide25Multiplier = 1374389535;
    private const int Divide25Shift = 35;

    /// <summary>
    /// The integer reciprocal and product shift for exact division by 143 on nonnegative signed integers.
    /// </summary>
    private const int Divide143Multiplier = 961111563;
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
    /// <param name="indices">The decoded signed index plane. The first output aliases this region.</param>
    /// <param name="palette">The explicit palette, with one row per output component. A zero-width palette needs no backing storage.</param>
    /// <param name="outputs">The allocated output regions in component order, including the reused index region first.</param>
    /// <param name="deltaCount">The number of explicit entries that contain prediction deltas.</param>
    /// <param name="predictor">The validated predictor code, from zero through thirteen.</param>
    /// <param name="bitDepth">The encoded sample precision used to scale implicit colors.</param>
    /// <param name="weightedPredictor">The group's weighted predictor coefficients.</param>
    /// <param name="scratch">Reusable weighted error storage large enough for an output row.</param>
    /// <param name="cancellationToken">The token checked between rows and output channels.</param>
    public static void Apply(
        Buffer2DRegion<int> indices,
        Buffer2DRegion<int> palette,
        ReadOnlySpan<Buffer2DRegion<int>> outputs,
        int deltaCount,
        byte predictor,
        int bitDepth,
        JxlWeightedPredictor weightedPredictor,
        Span<int> scratch,
        CancellationToken cancellationToken)
    {
        // Empty transformed channels retain their logical dimensions but have no samples or row storage to borrow.
        if (indices.Width == 0 || indices.Height == 0)
        {
            return;
        }

        // Restore the other channels before overwriting indices with output zero. This keeps one index image,
        // even for delta prediction: every output depends only on its own reconstructed neighbors.
        for (int i = 0; i < outputs.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int component = i == outputs.Length - 1 ? 0 : i + 1;
            Buffer2DRegion<int> output = outputs[component];
            ReadOnlySpan<int> table = palette.Width == 0 ? [] : palette.DangerousGetRowSpan(component);
            PaletteLookupOperator lookup = new(table, component, bitDepth);

            if (predictor == 0)
            {
                for (int y = 0; y < output.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyLookup(indices.DangerousGetRowSpan(y), output.DangerousGetRowSpan(y), in lookup);
                }
            }
            else if (predictor == 6)
            {
                // Weighted state advances for every sample, including absolute palette entries.
                // Reset the same caller-owned storage for each independent output channel.
                weightedPredictor.Reset(scratch, output.Width);
                ApplyWeighted(indices, output, deltaCount, weightedPredictor, scratch, in lookup, cancellationToken);
            }
            else
            {
                ApplyPredicted(indices, output, deltaCount, predictor, in lookup, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Expands independent index values with descending SIMD widths and a scalar remainder.
    /// </summary>
    /// <param name="indices">The input index row, which can alias the destination.</param>
    /// <param name="destination">The output component row.</param>
    /// <param name="lookup">The component's prepared palette arithmetic.</param>
    private static void ApplyLookup(ReadOnlySpan<int> indices, Span<int> destination, in PaletteLookupOperator lookup)
    {
        ref int source = ref MemoryMarshal.GetReference(indices);
        ref int output = ref MemoryMarshal.GetReference(destination);
        int x = 0;

        // Each lane is one independent column. Load its index before the store so output zero can reuse this row in place.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = indices.Length.Vector512Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector512<int>.Count)
            {
                Vector512<int> values = lookup.Invoke(Vector512.LoadUnsafe(ref source, (nuint)x));
                values.StoreUnsafe(ref output, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (indices.Length - x).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
            {
                Vector256<int> values = lookup.Invoke(Vector256.LoadUnsafe(ref source, (nuint)x));
                values.StoreUnsafe(ref output, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (indices.Length - x).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
            {
                Vector128<int> values = lookup.Invoke(Vector128.LoadUnsafe(ref source, (nuint)x));
                values.StoreUnsafe(ref output, (nuint)x);
            }
        }

        // Scalar lookup uses the same explicit range, negative cycle and implicit cube equations.
        for (; x < indices.Length; x++)
        {
            Unsafe.Add(ref output, x) = lookup.Invoke(Unsafe.Add(ref source, x));
        }
    }

    /// <summary>
    /// Restores delta entries with an existing nonweighted modular predictor.
    /// </summary>
    /// <param name="indices">The unchanged input indices, or the region being restored in place.</param>
    /// <param name="output">The current component's output region.</param>
    /// <param name="deltaCount">The first index that denotes an absolute entry.</param>
    /// <param name="predictor">The validated nonzero, nonweighted predictor.</param>
    /// <param name="lookup">The component's palette arithmetic.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void ApplyPredicted(
        Buffer2DRegion<int> indices,
        Buffer2DRegion<int> output,
        int deltaCount,
        byte predictor,
        in PaletteLookupOperator lookup,
        CancellationToken cancellationToken)
    {
        for (int y = 0; y < output.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<int> indexRow = indices.DangerousGetRowSpan(y);
            Span<int> row = output.DangerousGetRowSpan(y);
            ReadOnlySpan<int> topRow = y == 0 ? row : output.DangerousGetRowSpan(y - 1);
            ReadOnlySpan<int> aboveRow = y < 2 ? topRow : output.DangerousGetRowSpan(y - 2);

            for (int x = 0; x < row.Length; x++)
            {
                int index = indexRow[x];
                int value = lookup.Invoke(index);
                if (index < deltaCount)
                {
                    // Negative implicit indices are deltas too. Predict from already restored output,
                    // never from the index plane or from another output channel.
                    int left = x != 0 ? row[x - 1] : y != 0 ? topRow[0] : 0;
                    int top = y != 0 ? topRow[x] : left;
                    int topLeft = x != 0 && y != 0 ? topRow[x - 1] : left;
                    int topRight = y != 0 && x + 1 < row.Length ? topRow[x + 1] : top;
                    int leftLeft = x > 1 ? row[x - 2] : left;
                    int topTop = y > 1 ? aboveRow[x] : top;
                    int topRightRight = y != 0 && x + 2 < row.Length ? topRow[x + 2] : topRight;
                    long prediction = JxlModularPredictor.Predict(predictor, left, top, topLeft, topRight, leftLeft, topTop, topRightRight);
                    value = unchecked((int)(prediction + value));
                }

                row[x] = value;
            }
        }
    }

    /// <summary>
    /// Restores delta entries while advancing the weighted predictor for both absolute and delta samples.
    /// </summary>
    /// <param name="indices">The unchanged input indices, or the region being restored in place.</param>
    /// <param name="output">The current component's output region.</param>
    /// <param name="deltaCount">The first index that denotes an absolute entry.</param>
    /// <param name="weightedPredictor">The reset weighted predictor for this output channel.</param>
    /// <param name="scratch">The caller-owned error rows reused throughout this channel.</param>
    /// <param name="lookup">The component's palette arithmetic.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void ApplyWeighted(
        Buffer2DRegion<int> indices,
        Buffer2DRegion<int> output,
        int deltaCount,
        JxlWeightedPredictor weightedPredictor,
        Span<int> scratch,
        in PaletteLookupOperator lookup,
        CancellationToken cancellationToken)
    {
        for (int y = 0; y < output.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<int> indexRow = indices.DangerousGetRowSpan(y);
            Span<int> row = output.DangerousGetRowSpan(y);
            ReadOnlySpan<int> topRow = y == 0 ? row : output.DangerousGetRowSpan(y - 1);
            ReadOnlySpan<int> aboveRow = y < 2 ? topRow : output.DangerousGetRowSpan(y - 2);

            for (int x = 0; x < row.Length; x++)
            {
                int index = indexRow[x];
                int value = lookup.Invoke(index);
                int left = x != 0 ? row[x - 1] : y != 0 ? topRow[0] : 0;
                int top = y != 0 ? topRow[x] : left;
                int topLeft = x != 0 && y != 0 ? topRow[x - 1] : left;
                int topRight = y != 0 && x + 1 < row.Length ? topRow[x + 1] : top;
                int topTop = y > 1 ? aboveRow[x] : top;
                long prediction = weightedPredictor.Predict(scratch, x, y, left, top, topLeft, topRight, topTop, out _);
                if (index < deltaCount)
                {
                    value = unchecked((int)(prediction + value));
                }

                row[x] = value;
                weightedPredictor.Update(scratch, x, y, value);
            }
        }
    }

    /// <summary>
    /// Divides 4 nonnegative signed integers by a constant using exact widened products.
    /// </summary>
    /// <param name="values">Values from zero through int.MaxValue.</param>
    /// <param name="multiplier">The reciprocal numerator for one of the three palette divisors.</param>
    /// <param name="shift">The reciprocal denominator's base-two exponent.</param>
    /// <returns>The integer quotient in each original lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> DividePositive(Vector128<int> values, int multiplier, int shift)
    {
        // For divisor d and m=ceil(2^shift/d), (m*d-2^shift)*int.MaxValue < 2^shift proves exact truncation.
        // The three declared pairs satisfy this bound. No floating-point estimate or correction step is required.
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
    /// <param name="multiplier">The reciprocal numerator for one of the three palette divisors.</param>
    /// <param name="shift">The reciprocal denominator's base-two exponent.</param>
    /// <returns>The integer quotient in each original lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> DividePositive(Vector256<int> values, int multiplier, int shift)
    {
        // For divisor d and m=ceil(2^shift/d), (m*d-2^shift)*int.MaxValue < 2^shift proves exact truncation.
        // The three declared pairs satisfy this bound. No floating-point estimate or correction step is required.
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
    /// <param name="multiplier">The reciprocal numerator for one of the three palette divisors.</param>
    /// <param name="shift">The reciprocal denominator's base-two exponent.</param>
    /// <returns>The integer quotient in each original lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> DividePositive(Vector512<int> values, int multiplier, int shift)
    {
        // For divisor d and m=ceil(2^shift/d), (m*d-2^shift)*int.MaxValue < 2^shift proves exact truncation.
        // The three declared pairs satisfy this bound. No floating-point estimate or correction step is required.
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
        private readonly ReadOnlySpan<int> deltas;
        private readonly int component;
        private readonly int range;
        private readonly int deltaShift;
        private readonly int smallCubeOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteLookupOperator"/> struct for one output component.
        /// </summary>
        /// <param name="palette">The explicit entries for this component, or an empty span.</param>
        /// <param name="component">The component's zero-based position within this palette transform.</param>
        /// <param name="bitDepth">The encoded sample precision.</param>
        public PaletteLookupOperator(ReadOnlySpan<int> palette, int component, int bitDepth)
        {
            this.palette = palette;
            this.deltas = DeltaColors;
            this.component = component;

            // Implicit values use at most 24 bits. Explicit entries retain their decoded signed integer precision.
            // Cube products are at most 4*(2^24-1). Scaled delta magnitudes are at most 144*2^16. Both fit in int.
            int precision = Math.Min(bitDepth, MaximumImplicitPrecision);
            this.range = (1 << precision) - 1;
            this.deltaShift = Math.Max(precision - DeltaPrecision, 0);
            this.smallCubeOffset = 1 << Math.Max(precision - SmallCubeCenterShift, 0);
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
                // Complementing avoids negating int.MinValue. The 143-entry cycle alternates signs around 72 base colors.
                int delta = ~index % DeltaEntryCount;
                int value = this.deltas[(((delta + 1) >> 1) * RgbComponents) + this.component];
                return (value * (((delta & 1) << 1) - 1)) << this.deltaShift;
            }

            int cube = index - this.palette.Length;
            if (cube < SmallCubeEntries)
            {
                int digit = (cube >> (2 * this.component)) & 3;
                return ((digit * this.range) >> 2) + this.smallCubeOffset;
            }

            // The larger cube has five levels per component. Values beyond its 125 entries repeat by modulo five.
            int large = cube - SmallCubeEntries;
            int divisor = this.component == 0 ? 1 : this.component == 1 ? 5 : 25;
            return (((large / divisor) % 5) * this.range) >> 2;
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

            // Both reciprocal inputs are nonnegative and no greater than int.MaxValue, including complemented int.MinValue.
            Vector128<int> negative = Vector128.Max(~indices, Vector128<int>.Zero);
            Vector128<int> delta = negative - (DividePositive(negative, Divide143Multiplier, Divide143Shift) * Vector128.Create(DeltaEntryCount));
            Vector128<int> deltaIndices = (((delta + Vector128<int>.One) >> 1) * Vector128.Create(RgbComponents)) + Vector128.Create(this.component);
            Vector128<int> deltaValues = Vector128_.Gather(ref MemoryMarshal.GetReference(this.deltas), deltaIndices);
            deltaValues *= ((delta & Vector128<int>.One) << 1) - Vector128<int>.One;
            deltaValues <<= this.deltaShift;

            // Clamp only unused index arithmetic, not palette values. Valid implicit indices retain their full format-defined range.
            Vector128<int> nonnegative = Vector128.Max(indices, Vector128<int>.Zero);
            Vector128<int> cube = Vector128.Max(nonnegative - Vector128.Create(this.palette.Length), Vector128<int>.Zero);
            Vector128<int> digit = (cube >> (2 * this.component)) & Vector128.Create(3);
            Vector128<int> smallValues = ((digit * Vector128.Create(this.range)) >> 2) + Vector128.Create(this.smallCubeOffset);
            Vector128<int> large = Vector128.Max(cube - Vector128.Create(SmallCubeEntries), Vector128<int>.Zero);
            if (this.component != 0)
            {
                large = this.component == 1
                    ? DividePositive(large, Divide5Multiplier, Divide5Shift)
                    : DividePositive(large, Divide25Multiplier, Divide25Shift);
            }

            Vector128<int> largeDigit = large - (DividePositive(large, Divide5Multiplier, Divide5Shift) * Vector128.Create(5));
            Vector128<int> largeValues = (largeDigit * Vector128.Create(this.range)) >> 2;
            Vector128<int> implicitValues = Vector128.ConditionalSelect(
                Vector128.LessThan(cube, Vector128.Create(SmallCubeEntries)), smallValues, largeValues);

            implicitValues = Vector128.ConditionalSelect(Vector128.LessThan(indices, Vector128<int>.Zero), deltaValues, implicitValues);
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

            // Both reciprocal inputs are nonnegative and no greater than int.MaxValue, including complemented int.MinValue.
            Vector256<int> negative = Vector256.Max(~indices, Vector256<int>.Zero);
            Vector256<int> delta = negative - (DividePositive(negative, Divide143Multiplier, Divide143Shift) * Vector256.Create(DeltaEntryCount));
            Vector256<int> deltaIndices = (((delta + Vector256<int>.One) >> 1) * Vector256.Create(RgbComponents)) + Vector256.Create(this.component);
            Vector256<int> deltaValues = Vector256_.Gather(ref MemoryMarshal.GetReference(this.deltas), deltaIndices);
            deltaValues *= ((delta & Vector256<int>.One) << 1) - Vector256<int>.One;
            deltaValues <<= this.deltaShift;

            // Clamp only unused index arithmetic, not palette values. Valid implicit indices retain their full format-defined range.
            Vector256<int> nonnegative = Vector256.Max(indices, Vector256<int>.Zero);
            Vector256<int> cube = Vector256.Max(nonnegative - Vector256.Create(this.palette.Length), Vector256<int>.Zero);
            Vector256<int> digit = (cube >> (2 * this.component)) & Vector256.Create(3);
            Vector256<int> smallValues = ((digit * Vector256.Create(this.range)) >> 2) + Vector256.Create(this.smallCubeOffset);
            Vector256<int> large = Vector256.Max(cube - Vector256.Create(SmallCubeEntries), Vector256<int>.Zero);
            if (this.component != 0)
            {
                large = this.component == 1
                    ? DividePositive(large, Divide5Multiplier, Divide5Shift)
                    : DividePositive(large, Divide25Multiplier, Divide25Shift);
            }

            Vector256<int> largeDigit = large - (DividePositive(large, Divide5Multiplier, Divide5Shift) * Vector256.Create(5));
            Vector256<int> largeValues = (largeDigit * Vector256.Create(this.range)) >> 2;
            Vector256<int> implicitValues = Vector256.ConditionalSelect(
                Vector256.LessThan(cube, Vector256.Create(SmallCubeEntries)), smallValues, largeValues);

            implicitValues = Vector256.ConditionalSelect(Vector256.LessThan(indices, Vector256<int>.Zero), deltaValues, implicitValues);
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

            // Both reciprocal inputs are nonnegative and no greater than int.MaxValue, including complemented int.MinValue.
            Vector512<int> negative = Vector512.Max(~indices, Vector512<int>.Zero);
            Vector512<int> delta = negative - (DividePositive(negative, Divide143Multiplier, Divide143Shift) * Vector512.Create(DeltaEntryCount));
            Vector512<int> deltaIndices = (((delta + Vector512<int>.One) >> 1) * Vector512.Create(RgbComponents)) + Vector512.Create(this.component);
            Vector512<int> deltaValues = Vector512_.Gather(ref MemoryMarshal.GetReference(this.deltas), deltaIndices);
            deltaValues *= ((delta & Vector512<int>.One) << 1) - Vector512<int>.One;
            deltaValues <<= this.deltaShift;

            // Clamp only unused index arithmetic, not palette values. Valid implicit indices retain their full format-defined range.
            Vector512<int> nonnegative = Vector512.Max(indices, Vector512<int>.Zero);
            Vector512<int> cube = Vector512.Max(nonnegative - Vector512.Create(this.palette.Length), Vector512<int>.Zero);
            Vector512<int> digit = (cube >> (2 * this.component)) & Vector512.Create(3);
            Vector512<int> smallValues = ((digit * Vector512.Create(this.range)) >> 2) + Vector512.Create(this.smallCubeOffset);
            Vector512<int> large = Vector512.Max(cube - Vector512.Create(SmallCubeEntries), Vector512<int>.Zero);
            if (this.component != 0)
            {
                large = this.component == 1
                    ? DividePositive(large, Divide5Multiplier, Divide5Shift)
                    : DividePositive(large, Divide25Multiplier, Divide25Shift);
            }

            Vector512<int> largeDigit = large - (DividePositive(large, Divide5Multiplier, Divide5Shift) * Vector512.Create(5));
            Vector512<int> largeValues = (largeDigit * Vector512.Create(this.range)) >> 2;
            Vector512<int> implicitValues = Vector512.ConditionalSelect(
                Vector512.LessThan(cube, Vector512.Create(SmallCubeEntries)), smallValues, largeValues);

            implicitValues = Vector512.ConditionalSelect(Vector512.LessThan(indices, Vector512<int>.Zero), deltaValues, implicitValues);
            return Vector512.ConditionalSelect(explicitMask, explicitValues, implicitValues);
        }
    }
}

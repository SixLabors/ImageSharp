// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Provides modular prediction and prepares cross-channel properties from reconstructed samples.
/// </summary>
internal static class JxlModularPredictor
{
    /// <summary>
    /// Calculates one nonweighted modular predictor from already reconstructed signed neighbors.
    /// </summary>
    /// <param name="predictor">The validated nonweighted predictor code.</param>
    /// <param name="left">The preceding column, with the native edge fallback.</param>
    /// <param name="top">The preceding row at this column.</param>
    /// <param name="topLeft">The preceding row and column.</param>
    /// <param name="topRight">The preceding row and following column.</param>
    /// <param name="leftLeft">Two columns before the current sample.</param>
    /// <param name="topTop">Two rows before the current sample.</param>
    /// <param name="topRightRight">The preceding row and second following column.</param>
    /// <returns>The signed prediction before offset and residual reconstruction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Predict(byte predictor, long left, long top, long topLeft, long topRight, long leftLeft, long topTop, long topRightRight)
        => predictor switch
        {
            0 => 0,
            1 => left,
            2 => top,
            3 => (left + top) / 2,
            4 => Math.Abs(top - topLeft) < Math.Abs(left - topLeft) ? left : top,
            5 => Math.Clamp(left + top - topLeft, Math.Min(left, top), Math.Max(left, top)),
            7 => topRight,
            8 => topLeft,
            9 => leftLeft,
            10 => (left + topLeft) / 2,
            11 => (top + topLeft) / 2,
            12 => (top + topRight) / 2,
            _ => ((6 * top) - (2 * topTop) + (7 * left) + leftLeft + topRightRight + (3 * topRight) + 8) / 16
        };

    /// <summary>
    /// Prepares four cross-channel property planes for a bounded block of an already reconstructed channel.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int modular storage.</typeparam>
    /// <param name="row">The complete current source row.</param>
    /// <param name="topRow">The complete preceding source row when <paramref name="hasTop"/> is true.</param>
    /// <param name="start">The first source column in the block.</param>
    /// <param name="hasTop">Whether a preceding source row exists.</param>
    /// <param name="absoluteValues">The absolute-value properties. Its length defines the block size, at most 64 samples.</param>
    /// <param name="values">The signed-value properties, with the same length as <paramref name="absoluteValues"/>.</param>
    /// <param name="absoluteResiduals">The absolute gradient-residual properties, with the same block length.</param>
    /// <param name="residuals">The signed gradient-residual properties, with the same block length.</param>
    public static void PrepareReferences<TSample>(
        ReadOnlySpan<TSample> row,
        ReadOnlySpan<TSample> topRow,
        int start,
        bool hasTop,
        Span<int> absoluteValues,
        Span<int> values,
        Span<int> absoluteResiduals,
        Span<int> residuals)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref int absoluteValueRef = ref MemoryMarshal.GetReference(absoluteValues);
        ref int valueRef = ref MemoryMarshal.GetReference(values);
        ref int absoluteResidualRef = ref MemoryMarshal.GetReference(absoluteResiduals);
        ref int residualRef = ref MemoryMarshal.GetReference(residuals);
        int i = 0;

        // Cross-channel properties have their own native edge rule: left is zero at column zero, top at the first
        // row equals left, and top-left at either edge equals left. Handle column zero before loading shifted SIMD rows.
        if (start == 0 && !absoluteValues.IsEmpty)
        {
            int value = int.CreateTruncating(row[0]);
            ReferenceOperator.Apply(
                value, 0, hasTop ? int.CreateTruncating(topRow[0]) : 0, 0, out absoluteValues[0], out absoluteResiduals[0], out residuals[0]);

            values[0] = value;
            i = 1;
        }

        // Each lane holds one independent column from the earlier channel. Full source rows allow a block beginning
        // inside a row to read its true preceding sample. Unaligned loads support arbitrary block and group origins.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (absoluteValues.Length - i).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                nuint x = (nuint)(start + i);
                Vector512<int> value = JxlModularChannel.Load512(row, (int)x);
                Vector512<int> left = JxlModularChannel.Load512(row, (int)x - 1);
                Vector512<int> top = hasTop ? JxlModularChannel.Load512(topRow, (int)x) : left;
                Vector512<int> topLeft = hasTop ? JxlModularChannel.Load512(topRow, (int)x - 1) : left;
                ReferenceOperator.Apply(
                    value,
                    left,
                    top,
                    topLeft,
                    out Vector512<int> absolute,
                    out Vector512<int> absoluteResidual,
                    out Vector512<int> residual);

                absolute.StoreUnsafe(ref absoluteValueRef, (nuint)i);
                value.StoreUnsafe(ref valueRef, (nuint)i);
                absoluteResidual.StoreUnsafe(ref absoluteResidualRef, (nuint)i);
                residual.StoreUnsafe(ref residualRef, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (absoluteValues.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                nuint x = (nuint)(start + i);
                Vector256<int> value = JxlModularChannel.Load256(row, (int)x);
                Vector256<int> left = JxlModularChannel.Load256(row, (int)x - 1);
                Vector256<int> top = hasTop ? JxlModularChannel.Load256(topRow, (int)x) : left;
                Vector256<int> topLeft = hasTop ? JxlModularChannel.Load256(topRow, (int)x - 1) : left;
                ReferenceOperator.Apply(
                    value,
                    left,
                    top,
                    topLeft,
                    out Vector256<int> absolute,
                    out Vector256<int> absoluteResidual,
                    out Vector256<int> residual);

                absolute.StoreUnsafe(ref absoluteValueRef, (nuint)i);
                value.StoreUnsafe(ref valueRef, (nuint)i);
                absoluteResidual.StoreUnsafe(ref absoluteResidualRef, (nuint)i);
                residual.StoreUnsafe(ref residualRef, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (absoluteValues.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                nuint x = (nuint)(start + i);
                Vector128<int> value = JxlModularChannel.Load128(row, (int)x);
                Vector128<int> left = JxlModularChannel.Load128(row, (int)x - 1);
                Vector128<int> top = hasTop ? JxlModularChannel.Load128(topRow, (int)x) : left;
                Vector128<int> topLeft = hasTop ? JxlModularChannel.Load128(topRow, (int)x - 1) : left;
                ReferenceOperator.Apply(
                    value,
                    left,
                    top,
                    topLeft,
                    out Vector128<int> absolute,
                    out Vector128<int> absoluteResidual,
                    out Vector128<int> residual);

                absolute.StoreUnsafe(ref absoluteValueRef, (nuint)i);
                value.StoreUnsafe(ref valueRef, (nuint)i);
                absoluteResidual.StoreUnsafe(ref absoluteResidualRef, (nuint)i);
                residual.StoreUnsafe(ref residualRef, (nuint)i);
            }
        }

        // The same operator handles the final columns and complete blocks when hardware acceleration is disabled.
        for (; i < absoluteValues.Length; i++)
        {
            int x = start + i;
            int value = int.CreateTruncating(row[x]);
            int left = int.CreateTruncating(row[x - 1]);
            ReferenceOperator.Apply(
                value,
                left,
                hasTop ? int.CreateTruncating(topRow[x]) : left,
                hasTop ? int.CreateTruncating(topRow[x - 1]) : left,
                out absoluteValues[i],
                out absoluteResiduals[i],
                out residuals[i]);

            values[i] = value;
        }
    }

    /// <summary>
    /// Defines cross-channel gradient properties independently of block traversal.
    /// </summary>
    private readonly struct ReferenceOperator
    {
        /// <summary>
        /// Calculates one clamped-gradient residual with wide arithmetic before storing signed 32-bit properties.
        /// </summary>
        /// <param name="value">The reconstructed sample.</param>
        /// <param name="left">The preceding sample.</param>
        /// <param name="top">The sample above.</param>
        /// <param name="topLeft">The sample above and to the left.</param>
        /// <param name="absolute">The absolute sample property, stored with signed 32-bit wrapping.</param>
        /// <param name="absoluteResidual">The absolute gradient-residual property, stored with signed 32-bit wrapping.</param>
        /// <param name="residual">The signed gradient-residual property, stored with signed 32-bit wrapping.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(int value, int left, int top, int topLeft, out int absolute, out int absoluteResidual, out int residual)
        {
            long guess = Math.Clamp((long)left + top - topLeft, Math.Min(left, top), Math.Max(left, top));
            long difference = value - guess;
            absolute = unchecked((int)Math.Abs((long)value));
            absoluteResidual = unchecked((int)Math.Abs(difference));
            residual = unchecked((int)difference);
        }

        /// <summary>
        /// Calculates independent cross-channel properties in 4 signed sample lanes.
        /// </summary>
        /// <param name="value">The reconstructed sample lanes.</param>
        /// <param name="left">The preceding-sample lanes.</param>
        /// <param name="top">The above-sample lanes.</param>
        /// <param name="topLeft">The above-left-sample lanes.</param>
        /// <param name="absolute">The absolute sample properties.</param>
        /// <param name="absoluteResidual">The absolute gradient-residual properties.</param>
        /// <param name="residual">The signed gradient-residual properties.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            Vector128<int> value,
            Vector128<int> left,
            Vector128<int> top,
            Vector128<int> topLeft,
            out Vector128<int> absolute,
            out Vector128<int> absoluteResidual,
            out Vector128<int> residual)
        {
            Vector128<long> lower = Residual(
                Vector128.WidenLower(value), Vector128.WidenLower(left), Vector128.WidenLower(top), Vector128.WidenLower(topLeft));

            Vector128<long> upper = Residual(
                Vector128.WidenUpper(value), Vector128.WidenUpper(left), Vector128.WidenUpper(top), Vector128.WidenUpper(topLeft));

            // Take the residual magnitude before narrowing. A wide positive residual above Int32.MaxValue must
            // retain its low 32 bits, not become the absolute value of a prematurely narrowed negative integer.
            absolute = Vector128.Abs(value);
            absoluteResidual = Vector128.Narrow(Vector128.Abs(lower), Vector128.Abs(upper));
            residual = Vector128.Narrow(lower, upper);
        }

        /// <summary>
        /// Calculates wide residual lanes after bounding the gradient to its two adjacent reconstructed samples.
        /// </summary>
        /// <param name="value">The widened reconstructed samples.</param>
        /// <param name="left">The widened preceding samples.</param>
        /// <param name="top">The widened above samples.</param>
        /// <param name="topLeft">The widened above-left samples.</param>
        /// <returns>The wide gradient residuals before property storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<long> Residual(Vector128<long> value, Vector128<long> left, Vector128<long> top, Vector128<long> topLeft)
        {
            // The neighbors are signed 32-bit samples. Their sum and difference need wide lanes before clamping;
            // the bounded guess then remains between left and top, while its residual can span 33 signed bits.
            Vector128<long> guess = Vector128.Clamp(left + top - topLeft, Vector128.Min(left, top), Vector128.Max(left, top));
            return value - guess;
        }

        /// <summary>
        /// Calculates independent cross-channel properties in 8 signed sample lanes.
        /// </summary>
        /// <param name="value">The reconstructed sample lanes.</param>
        /// <param name="left">The preceding-sample lanes.</param>
        /// <param name="top">The above-sample lanes.</param>
        /// <param name="topLeft">The above-left-sample lanes.</param>
        /// <param name="absolute">The absolute sample properties.</param>
        /// <param name="absoluteResidual">The absolute gradient-residual properties.</param>
        /// <param name="residual">The signed gradient-residual properties.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            Vector256<int> value,
            Vector256<int> left,
            Vector256<int> top,
            Vector256<int> topLeft,
            out Vector256<int> absolute,
            out Vector256<int> absoluteResidual,
            out Vector256<int> residual)
        {
            Vector256<long> lower = Residual(
                Vector256.WidenLower(value), Vector256.WidenLower(left), Vector256.WidenLower(top), Vector256.WidenLower(topLeft));

            Vector256<long> upper = Residual(
                Vector256.WidenUpper(value), Vector256.WidenUpper(left), Vector256.WidenUpper(top), Vector256.WidenUpper(topLeft));

            // Take the residual magnitude before narrowing. A wide positive residual above Int32.MaxValue must
            // retain its low 32 bits, not become the absolute value of a prematurely narrowed negative integer.
            absolute = Vector256.Abs(value);
            absoluteResidual = Vector256.Narrow(Vector256.Abs(lower), Vector256.Abs(upper));
            residual = Vector256.Narrow(lower, upper);
        }

        /// <summary>
        /// Calculates wide residual lanes after bounding the gradient to its two adjacent reconstructed samples.
        /// </summary>
        /// <param name="value">The widened reconstructed samples.</param>
        /// <param name="left">The widened preceding samples.</param>
        /// <param name="top">The widened above samples.</param>
        /// <param name="topLeft">The widened above-left samples.</param>
        /// <returns>The wide gradient residuals before property storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<long> Residual(Vector256<long> value, Vector256<long> left, Vector256<long> top, Vector256<long> topLeft)
        {
            // The neighbors are signed 32-bit samples. Their sum and difference need wide lanes before clamping;
            // the bounded guess then remains between left and top, while its residual can span 33 signed bits.
            Vector256<long> guess = Vector256.Clamp(left + top - topLeft, Vector256.Min(left, top), Vector256.Max(left, top));
            return value - guess;
        }

        /// <summary>
        /// Calculates independent cross-channel properties in 16 signed sample lanes.
        /// </summary>
        /// <param name="value">The reconstructed sample lanes.</param>
        /// <param name="left">The preceding-sample lanes.</param>
        /// <param name="top">The above-sample lanes.</param>
        /// <param name="topLeft">The above-left-sample lanes.</param>
        /// <param name="absolute">The absolute sample properties.</param>
        /// <param name="absoluteResidual">The absolute gradient-residual properties.</param>
        /// <param name="residual">The signed gradient-residual properties.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            Vector512<int> value,
            Vector512<int> left,
            Vector512<int> top,
            Vector512<int> topLeft,
            out Vector512<int> absolute,
            out Vector512<int> absoluteResidual,
            out Vector512<int> residual)
        {
            Vector512<long> lower = Residual(
                Vector512.WidenLower(value), Vector512.WidenLower(left), Vector512.WidenLower(top), Vector512.WidenLower(topLeft));

            Vector512<long> upper = Residual(
                Vector512.WidenUpper(value), Vector512.WidenUpper(left), Vector512.WidenUpper(top), Vector512.WidenUpper(topLeft));

            // Take the residual magnitude before narrowing. A wide positive residual above Int32.MaxValue must
            // retain its low 32 bits, not become the absolute value of a prematurely narrowed negative integer.
            absolute = Vector512.Abs(value);
            absoluteResidual = Vector512.Narrow(Vector512.Abs(lower), Vector512.Abs(upper));
            residual = Vector512.Narrow(lower, upper);
        }

        /// <summary>
        /// Calculates wide residual lanes after bounding the gradient to its two adjacent reconstructed samples.
        /// </summary>
        /// <param name="value">The widened reconstructed samples.</param>
        /// <param name="left">The widened preceding samples.</param>
        /// <param name="top">The widened above samples.</param>
        /// <param name="topLeft">The widened above-left samples.</param>
        /// <returns>The wide gradient residuals before property storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<long> Residual(Vector512<long> value, Vector512<long> left, Vector512<long> top, Vector512<long> topLeft)
        {
            // The neighbors are signed 32-bit samples. Their sum and difference need wide lanes before clamping;
            // the bounded guess then remains between left and top, while its residual can span 33 signed bits.
            Vector512<long> guess = Vector512.Clamp(left + top - topLeft, Vector512.Min(left, top), Vector512.Max(left, top));
            return value - guess;
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Interpolates a sampled curve, including its inverse, across independent component lanes.
/// </summary>
internal readonly struct LutOperator : TensorPrimitives_.IUnaryOperator
{
    private readonly float[] table;
    private readonly bool inverse;
    private readonly bool identity;
    private readonly int scale;

    /// <summary>
    /// Initializes a new instance of the <see cref="LutOperator"/> struct.
    /// </summary>
    /// <param name="table">The sampled curve with at least two entries.</param>
    /// <param name="inverse">Whether to find curve inputs from their output values.</param>
    public LutOperator(float[] table, bool inverse)
        : this(table, inverse, table.Length)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LutOperator"/> struct with a logical forward sample count.
    /// </summary>
    /// <param name="table">The sampled curve, including any padding after its final logical sample.</param>
    /// <param name="inverse">Whether to search the complete physical table for inverse conversion.</param>
    /// <param name="sampleCount">The forward sample count, from two through <paramref name="table"/>.Length, covering inputs zero through one.</param>
    public LutOperator(float[] table, bool inverse, int sampleCount)
    {
        this.table = table;
        this.inverse = inverse;

        // ICC curves use every stored sample. Companding retains one padding sample after input one;
        // its logical count sets the coordinate scale while physical bounds keep the neighboring read valid.
        this.scale = sampleCount - 1;

        // A two-point [0,1] forward table is bounded identity. Keep its input bounds instead of doing two gathers.
        this.identity = !inverse && table.Length == 2 && table[0] == 0F && table[1] == 1F;
    }

    /// <inheritdoc/>
    public float Invoke(float value) => this.Convert(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<float> Invoke(Vector128<float> value) => this.Convert(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector256<float> Invoke(Vector256<float> value) => this.Convert(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector512<float> Invoke(Vector512<float> value) => this.Convert(value);

    /// <summary>
    /// Interpolates one curve sample using the same endpoint and inverse-search rules as the vector paths.
    /// </summary>
    /// <param name="value">The curve input, or its output for an inverse lookup.</param>
    /// <returns>The interpolated curve value.</returns>
    public float Convert(float value)
    {
        if (!this.inverse)
        {
            return this.ConvertForward(value);
        }

        int found = Array.BinarySearch(this.table, value);
        if (found >= 0)
        {
            return found / (float)(this.table.Length - 1);
        }

        int insertion = ~found;
        if (insertion == 0)
        {
            return 0F;
        }

        if (insertion == this.table.Length)
        {
            return 1F;
        }

        float high = this.table[insertion];
        float low = this.table[insertion - 1];
        float fraction = (value - low) / (high - low);
        float range = 1F / (this.table.Length - 1);
        return ((insertion - 1) / (float)(this.table.Length - 1)) + (fraction * range);
    }

    /// <summary>
    /// Interpolates one forward sample with the same endpoints as the SIMD paths.
    /// </summary>
    /// <param name="value">The lookup coordinate.</param>
    /// <returns>The interpolated sample.</returns>
    public float ConvertForward(float value)
    {
        if (this.identity)
        {
            // Adding zero retains the table interpolation's positive zero for an input of negative zero.
            return 0F + ColorNumerics.ClampToFinite(value, 0F, 1F);
        }

        float bounded = ColorNumerics.ClampToFinite(value, 0F, 1F);

        if (bounded == 1F)
        {
            // Input one selects the final logical sample, not any padding that follows it.
            // Return its integer-indexed value directly because large indices can round when converted to float.
            // Adding zero retains interpolation's positive-zero result for a negative-zero sample.
            return 0F + this.table[this.scale];
        }

        float position = bounded * this.scale;
        int index = Math.Min((int)position, this.table.Length - 1);
        float lower = this.table[index];
        float upper = index < this.table.Length - 1 ? this.table[index + 1] : 1F;

        return lower + ((upper - lower) * (position - index));
    }

    /// <summary>
    /// Interpolates 4 curve samples in independent lanes.
    /// </summary>
    /// <param name="value">The curve inputs, or their outputs for an inverse lookup.</param>
    /// <returns>The interpolated values in the original lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Vector128<float> Convert(Vector128<float> value)
    {
        if (!this.inverse)
        {
            return this.ConvertForward(value);
        }

        // The inverse search owns its pin for the complete search.
        fixed (float* table = this.table)
        {
            return this.ConvertInverse((int*)table, value);
        }
    }

    /// <summary>
    /// Interpolates forward samples without a per-block direction decision.
    /// </summary>
    /// <param name="value">The normalized lookup coordinates in independent lanes.</param>
    /// <returns>The interpolated samples in the same lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Vector128<float> ConvertForward(Vector128<float> value)
    {
        if (this.identity)
        {
            // Ordered comparisons map NaN to zero, as the sampled path does. Addition clears negative zero.
            value = Vector128.ConditionalSelect(Vector128.GreaterThan(value, Vector128<float>.One), Vector128<float>.One, value);
            value = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(value, Vector128<float>.Zero), value, Vector128<float>.Zero);
            return Vector128<float>.Zero + value;
        }

        // One pin covers both neighboring samples for every lane.
        fixed (float* table = this.table)
        {
            Vector128<float> scale = Vector128.Create((float)this.scale);
            Vector128<int> last = Vector128.Create(this.table.Length - 1);

            // Ordered comparisons map NaN to zero, matching ColorNumerics.ClampToFinite.
            Vector128<float> bounded = Vector128.ConditionalSelect(Vector128.GreaterThan(value, Vector128<float>.One), Vector128<float>.One, value);
            bounded = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(bounded, Vector128<float>.Zero), bounded, Vector128<float>.Zero);
            Vector128<float> position = bounded * scale;

            // Sampled ICC tables fit in the profile payload. Clamping above leaves finite positions
            // inside that table, so integer conversion needs no NaN or overflow saturation.
            // A last index above 2^24 may round when converted to float: 16,777,217 becomes 16,777,216.
            Vector128<int> index = Vector128.Min(Vector128.ConvertToInt32Native(position), last);

            // Input one selects the final logical sample, not any padding that follows it.
            // Its integer index can differ from the rounded float position. Select the exact endpoint per lane.
            // Converting that endpoint back to float gives the same position, so its interpolation fraction is zero.
            index = Vector128.ConditionalSelect(Vector128.Equals(bounded, Vector128<float>.One).AsInt32(), Vector128.Create(this.scale), index);

            // Gather integer words and reinterpret their float bits; no numeric integer conversion occurs.
            // The fraction position-index weights adjacent samples. Limit the second read for lanes at the final physical sample.
            Vector128<float> lower = Vector128_.Gather((int*)table, index).AsSingle();
            Vector128<float> upper = Vector128_.Gather((int*)table, Vector128.Min(index + Vector128<int>.One, last)).AsSingle();
            upper = Vector128.ConditionalSelect(Vector128.LessThan(index, last).AsSingle(), upper, Vector128<float>.One);
            return lower + ((upper - lower) * (position - Vector128.ConvertToSingle(index)));
        }
    }

    /// <summary>
    /// Searches 4 inverse curve values while the caller holds the table pin.
    /// Keep binary search separate so forward lookup callers do not inline it.
    /// </summary>
    /// <param name="table">The pinned curve table words.</param>
    /// <param name="value">The curve outputs whose inputs must be found.</param>
    /// <returns>The interpolated results in lane order.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe Vector128<float> ConvertInverse(int* table, Vector128<float> value)
    {
        Vector128<float> scale = Vector128.Create((float)(this.table.Length - 1));
        Vector128<int> last = Vector128.Create(this.table.Length - 1);

        Vector128<int> low = Vector128<int>.Zero;
        Vector128<int> high = last;
        Vector128<int> found = Vector128<int>.Zero;
        Vector128<float> exact = Vector128<float>.Zero;

        // Each lane follows Array.BinarySearch independently. Completed lanes leave the search while others continue.
        // Clamp their unused gather indices too: SIMD evaluates every lane, including those that are already finished.
        while (Vector128.LessThanOrEqualAny(low, high))
        {
            Vector128<int> active = Vector128.LessThanOrEqual(low, high);
            Vector128<int> middle = low + ((high - low) >> 1);
            middle = Vector128.Min(Vector128.Max(middle, Vector128<int>.Zero), last);
            Vector128<float> sample = Vector128_.Gather(table, middle).AsSingle();
            Vector128<int> equal = Vector128.Equals(sample, value).AsInt32() & active;
            found |= equal;
            exact = Vector128.ConditionalSelect(equal.AsSingle(), Vector128.ConvertToSingle(middle) / scale, exact);
            Vector128<int> advance = Vector128.LessThan(sample, value).AsInt32() & active;
            low = Vector128.ConditionalSelect(advance, middle + Vector128<int>.One, low);
            high = Vector128.ConditionalSelect(active & ~advance, middle - Vector128<int>.One, high);

            // Mark an exact match complete with low = 0 and high = -1. Other lanes continue searching their own intervals.
            low = Vector128.ConditionalSelect(equal, Vector128<int>.Zero, low);
            high = Vector128.ConditionalSelect(equal, Vector128.Create(-1), high);
        }

        // Unmatched lanes now contain insertion indices. Interpolate the bracketing samples, then select endpoints
        // and exact matches. The clamped index keeps both neighbor reads valid even for endpoint and completed lanes.
        Vector128<int> insertion = Vector128.Min(Vector128.Max(low, Vector128<int>.One), last);
        Vector128<float> lowerValue = Vector128_.Gather(table, insertion - Vector128<int>.One).AsSingle();
        Vector128<float> upperValue = Vector128_.Gather(table, insertion).AsSingle();
        Vector128<float> percent = (value - lowerValue) / (upperValue - lowerValue);
        Vector128<float> result = (Vector128.ConvertToSingle(insertion - Vector128<int>.One) / scale) + (percent * (Vector128<float>.One / scale));
        result = Vector128.ConditionalSelect(Vector128.Equals(low, Vector128<int>.Zero).AsSingle(), Vector128<float>.Zero, result);
        result = Vector128.ConditionalSelect(Vector128.GreaterThan(low, last).AsSingle(), Vector128<float>.One, result);
        return Vector128.ConditionalSelect(found.AsSingle(), exact, result);
    }

    /// <summary>
    /// Interpolates 8 curve samples in independent lanes.
    /// </summary>
    /// <param name="value">The curve inputs, or their outputs for an inverse lookup.</param>
    /// <returns>The interpolated values in the original lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Vector256<float> Convert(Vector256<float> value)
    {
        if (!this.inverse)
        {
            return this.ConvertForward(value);
        }

        // The inverse search owns its pin for the complete search.
        fixed (float* table = this.table)
        {
            return this.ConvertInverse((int*)table, value);
        }
    }

    /// <summary>
    /// Interpolates forward samples without a per-block direction decision.
    /// </summary>
    /// <param name="value">The normalized lookup coordinates in independent lanes.</param>
    /// <returns>The interpolated samples in the same lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Vector256<float> ConvertForward(Vector256<float> value)
    {
        if (this.identity)
        {
            // Ordered comparisons map NaN to zero, as the sampled path does. Addition clears negative zero.
            value = Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256<float>.One), Vector256<float>.One, value);
            value = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(value, Vector256<float>.Zero), value, Vector256<float>.Zero);
            return Vector256<float>.Zero + value;
        }

        // One pin covers both neighboring samples for every lane.
        fixed (float* table = this.table)
        {
            Vector256<float> scale = Vector256.Create((float)this.scale);
            Vector256<int> last = Vector256.Create(this.table.Length - 1);

            // Ordered comparisons map NaN to zero, matching ColorNumerics.ClampToFinite.
            Vector256<float> bounded = Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256<float>.One), Vector256<float>.One, value);
            bounded = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(bounded, Vector256<float>.Zero), bounded, Vector256<float>.Zero);
            Vector256<float> position = bounded * scale;

            // Sampled ICC tables fit in the profile payload. Clamping above leaves finite positions
            // inside that table, so integer conversion needs no NaN or overflow saturation.
            // A last index above 2^24 may round when converted to float: 16,777,217 becomes 16,777,216.
            Vector256<int> index = Vector256.Min(Vector256.ConvertToInt32Native(position), last);

            // Input one selects the final logical sample, not any padding that follows it.
            // Its integer index can differ from the rounded float position. Select the exact endpoint per lane.
            // Converting that endpoint back to float gives the same position, so its interpolation fraction is zero.
            index = Vector256.ConditionalSelect(Vector256.Equals(bounded, Vector256<float>.One).AsInt32(), Vector256.Create(this.scale), index);

            // Gather integer words and reinterpret their float bits; no numeric integer conversion occurs.
            // The fraction position-index weights adjacent samples. Limit the second read for lanes at the final physical sample.
            Vector256<float> lower = Vector256_.Gather((int*)table, index).AsSingle();
            Vector256<float> upper = Vector256_.Gather((int*)table, Vector256.Min(index + Vector256<int>.One, last)).AsSingle();
            upper = Vector256.ConditionalSelect(Vector256.LessThan(index, last).AsSingle(), upper, Vector256<float>.One);
            return lower + ((upper - lower) * (position - Vector256.ConvertToSingle(index)));
        }
    }

    /// <summary>
    /// Searches 8 inverse curve values while the caller holds the table pin.
    /// Keep binary search separate so forward lookup callers do not inline it.
    /// </summary>
    /// <param name="table">The pinned curve table words.</param>
    /// <param name="value">The curve outputs whose inputs must be found.</param>
    /// <returns>The interpolated results in lane order.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe Vector256<float> ConvertInverse(int* table, Vector256<float> value)
    {
        Vector256<float> scale = Vector256.Create((float)(this.table.Length - 1));
        Vector256<int> last = Vector256.Create(this.table.Length - 1);

        Vector256<int> low = Vector256<int>.Zero;
        Vector256<int> high = last;
        Vector256<int> found = Vector256<int>.Zero;
        Vector256<float> exact = Vector256<float>.Zero;

        // Each lane follows Array.BinarySearch independently. Completed lanes leave the search while others continue.
        // Clamp their unused gather indices too: SIMD evaluates every lane, including those that are already finished.
        while (Vector256.LessThanOrEqualAny(low, high))
        {
            Vector256<int> active = Vector256.LessThanOrEqual(low, high);
            Vector256<int> middle = low + ((high - low) >> 1);
            middle = Vector256.Min(Vector256.Max(middle, Vector256<int>.Zero), last);
            Vector256<float> sample = Vector256_.Gather(table, middle).AsSingle();
            Vector256<int> equal = Vector256.Equals(sample, value).AsInt32() & active;
            found |= equal;
            exact = Vector256.ConditionalSelect(equal.AsSingle(), Vector256.ConvertToSingle(middle) / scale, exact);
            Vector256<int> advance = Vector256.LessThan(sample, value).AsInt32() & active;
            low = Vector256.ConditionalSelect(advance, middle + Vector256<int>.One, low);
            high = Vector256.ConditionalSelect(active & ~advance, middle - Vector256<int>.One, high);

            // Mark an exact match complete with low = 0 and high = -1. Other lanes continue searching their own intervals.
            low = Vector256.ConditionalSelect(equal, Vector256<int>.Zero, low);
            high = Vector256.ConditionalSelect(equal, Vector256.Create(-1), high);
        }

        // Unmatched lanes now contain insertion indices. Interpolate the bracketing samples, then select endpoints
        // and exact matches. The clamped index keeps both neighbor reads valid even for endpoint and completed lanes.
        Vector256<int> insertion = Vector256.Min(Vector256.Max(low, Vector256<int>.One), last);
        Vector256<float> lowerValue = Vector256_.Gather(table, insertion - Vector256<int>.One).AsSingle();
        Vector256<float> upperValue = Vector256_.Gather(table, insertion).AsSingle();
        Vector256<float> percent = (value - lowerValue) / (upperValue - lowerValue);
        Vector256<float> result = (Vector256.ConvertToSingle(insertion - Vector256<int>.One) / scale) + (percent * (Vector256<float>.One / scale));
        result = Vector256.ConditionalSelect(Vector256.Equals(low, Vector256<int>.Zero).AsSingle(), Vector256<float>.Zero, result);
        result = Vector256.ConditionalSelect(Vector256.GreaterThan(low, last).AsSingle(), Vector256<float>.One, result);
        return Vector256.ConditionalSelect(found.AsSingle(), exact, result);
    }

    /// <summary>
    /// Interpolates 16 curve samples in independent lanes.
    /// </summary>
    /// <param name="value">The curve inputs, or their outputs for an inverse lookup.</param>
    /// <returns>The interpolated values in the original lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Vector512<float> Convert(Vector512<float> value)
    {
        if (!this.inverse)
        {
            return this.ConvertForward(value);
        }

        // The inverse search owns its pin for the complete search.
        fixed (float* table = this.table)
        {
            return this.ConvertInverse((int*)table, value);
        }
    }

    /// <summary>
    /// Interpolates forward samples without a per-block direction decision.
    /// </summary>
    /// <param name="value">The normalized lookup coordinates in independent lanes.</param>
    /// <returns>The interpolated samples in the same lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Vector512<float> ConvertForward(Vector512<float> value)
    {
        if (this.identity)
        {
            // Ordered comparisons map NaN to zero, as the sampled path does. Addition clears negative zero.
            value = Vector512.ConditionalSelect(Vector512.GreaterThan(value, Vector512<float>.One), Vector512<float>.One, value);
            value = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(value, Vector512<float>.Zero), value, Vector512<float>.Zero);
            return Vector512<float>.Zero + value;
        }

        // One pin covers both neighboring samples for every lane.
        fixed (float* table = this.table)
        {
            Vector512<float> scale = Vector512.Create((float)this.scale);
            Vector512<int> last = Vector512.Create(this.table.Length - 1);

            // Ordered comparisons map NaN to zero, matching ColorNumerics.ClampToFinite.
            Vector512<float> bounded = Vector512.ConditionalSelect(Vector512.GreaterThan(value, Vector512<float>.One), Vector512<float>.One, value);
            bounded = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(bounded, Vector512<float>.Zero), bounded, Vector512<float>.Zero);
            Vector512<float> position = bounded * scale;

            // Sampled ICC tables fit in the profile payload. Clamping above leaves finite positions
            // inside that table, so integer conversion needs no NaN or overflow saturation.
            // A last index above 2^24 may round when converted to float: 16,777,217 becomes 16,777,216.
            Vector512<int> index = Vector512.Min(Vector512.ConvertToInt32Native(position), last);

            // Input one selects the final logical sample, not any padding that follows it.
            // Its integer index can differ from the rounded float position. Select the exact endpoint per lane.
            // Converting that endpoint back to float gives the same position, so its interpolation fraction is zero.
            index = Vector512.ConditionalSelect(Vector512.Equals(bounded, Vector512<float>.One).AsInt32(), Vector512.Create(this.scale), index);

            // Gather integer words and reinterpret their float bits; no numeric integer conversion occurs.
            // The fraction position-index weights adjacent samples. Limit the second read for lanes at the final physical sample.
            Vector512<float> lower = Vector512_.Gather((int*)table, index).AsSingle();
            Vector512<float> upper = Vector512_.Gather((int*)table, Vector512.Min(index + Vector512<int>.One, last)).AsSingle();
            upper = Vector512.ConditionalSelect(Vector512.LessThan(index, last).AsSingle(), upper, Vector512<float>.One);
            return lower + ((upper - lower) * (position - Vector512.ConvertToSingle(index)));
        }
    }

    /// <summary>
    /// Searches 16 inverse curve values while the caller holds the table pin.
    /// Keep binary search separate so forward lookup callers do not inline it.
    /// </summary>
    /// <param name="table">The pinned curve table words.</param>
    /// <param name="value">The curve outputs whose inputs must be found.</param>
    /// <returns>The interpolated results in lane order.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe Vector512<float> ConvertInverse(int* table, Vector512<float> value)
    {
        Vector512<float> scale = Vector512.Create((float)(this.table.Length - 1));
        Vector512<int> last = Vector512.Create(this.table.Length - 1);

        Vector512<int> low = Vector512<int>.Zero;
        Vector512<int> high = last;
        Vector512<int> found = Vector512<int>.Zero;
        Vector512<float> exact = Vector512<float>.Zero;

        // Each lane follows Array.BinarySearch independently. Completed lanes leave the search while others continue.
        // Clamp their unused gather indices too: SIMD evaluates every lane, including those that are already finished.
        while (Vector512.LessThanOrEqualAny(low, high))
        {
            Vector512<int> active = Vector512.LessThanOrEqual(low, high);
            Vector512<int> middle = low + ((high - low) >> 1);
            middle = Vector512.Min(Vector512.Max(middle, Vector512<int>.Zero), last);
            Vector512<float> sample = Vector512_.Gather(table, middle).AsSingle();
            Vector512<int> equal = Vector512.Equals(sample, value).AsInt32() & active;
            found |= equal;
            exact = Vector512.ConditionalSelect(equal.AsSingle(), Vector512.ConvertToSingle(middle) / scale, exact);
            Vector512<int> advance = Vector512.LessThan(sample, value).AsInt32() & active;
            low = Vector512.ConditionalSelect(advance, middle + Vector512<int>.One, low);
            high = Vector512.ConditionalSelect(active & ~advance, middle - Vector512<int>.One, high);

            // Mark an exact match complete with low = 0 and high = -1. Other lanes continue searching their own intervals.
            low = Vector512.ConditionalSelect(equal, Vector512<int>.Zero, low);
            high = Vector512.ConditionalSelect(equal, Vector512.Create(-1), high);
        }

        // Unmatched lanes now contain insertion indices. Interpolate the bracketing samples, then select endpoints
        // and exact matches. The clamped index keeps both neighbor reads valid even for endpoint and completed lanes.
        Vector512<int> insertion = Vector512.Min(Vector512.Max(low, Vector512<int>.One), last);
        Vector512<float> lowerValue = Vector512_.Gather(table, insertion - Vector512<int>.One).AsSingle();
        Vector512<float> upperValue = Vector512_.Gather(table, insertion).AsSingle();
        Vector512<float> percent = (value - lowerValue) / (upperValue - lowerValue);
        Vector512<float> result = (Vector512.ConvertToSingle(insertion - Vector512<int>.One) / scale) + (percent * (Vector512<float>.One / scale));
        result = Vector512.ConditionalSelect(Vector512.Equals(low, Vector512<int>.Zero).AsSingle(), Vector512<float>.Zero, result);
        result = Vector512.ConditionalSelect(Vector512.GreaterThan(low, last).AsSingle(), Vector512<float>.One, result);
        return Vector512.ConditionalSelect(found.AsSingle(), exact, result);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Interpolates a sampled ICC curve, including its inverse, across independent color lanes.
    /// </summary>
    internal readonly struct LutOperator
    {
        private readonly float[] table;
        private readonly bool inverse;

        /// <summary>
        /// Initializes a new instance of the <see cref="LutOperator"/> struct.
        /// </summary>
        /// <param name="table">The sampled curve with at least two entries.</param>
        /// <param name="inverse">Whether to find curve inputs from their output values.</param>
        public LutOperator(float[] table, bool inverse)
        {
            // LUT16 table counts come from the profile. Validate them before any SIMD gather can read a neighbor.
            // Zero-entry and one-entry curve tags are identity and gamma curves, handled by the curve operator.
            if (table.Length < 2)
            {
                throw new InvalidIccProfileException("Sampled ICC tables require at least two entries.");
            }

            this.table = table;
            this.inverse = inverse;
        }

        /// <summary>
        /// Interpolates one curve sample using the same endpoint and inverse-search rules as the vector paths.
        /// </summary>
        /// <param name="value">The curve input, or its output for an inverse lookup.</param>
        /// <returns>The interpolated curve value.</returns>
        public float Convert(float value)
        {
            if (!this.inverse)
            {
                float position = Numerics.Clamp(value, 0F, 1F) * (this.table.Length - 1);
                int index = (int)position;
                float lower = this.table[index];
                float upper = index < this.table.Length - 1 ? this.table[index + 1] : 1F;
                return lower + ((upper - lower) * (position - index));
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
        /// Interpolates 4 curve samples in independent lanes.
        /// </summary>
        /// <param name="value">The curve inputs, or their outputs for an inverse lookup.</param>
        /// <returns>The interpolated values in the original lane order.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Vector128<float> Convert(Vector128<float> value)
        {
            // Gather copies 32-bit words unchanged. Reinterpret the float table, then restore the float lane type.
            ref int table = ref Unsafe.As<float, int>(ref MemoryMarshal.GetArrayDataReference(this.table));
            Vector128<float> scale = Vector128.Create((float)(this.table.Length - 1));
            Vector128<int> last = Vector128.Create(this.table.Length - 1);

            if (!this.inverse)
            {
                // Ordered comparisons map NaN to zero, matching Numerics.Clamp. The last cell repeats its endpoint.
                Vector128<float> bounded = Vector128.ConditionalSelect(Vector128.GreaterThan(value, Vector128<float>.One), Vector128<float>.One, value);
                bounded = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(bounded, Vector128<float>.Zero), bounded, Vector128<float>.Zero);
                Vector128<float> position = bounded * scale;
                Vector128<int> index = Vector128.ConvertToInt32(position);
                Vector128<float> lower = Vector128_.Gather(ref table, index).AsSingle();
                Vector128<float> upper = Vector128_.Gather(ref table, Vector128.Min(index + Vector128<int>.One, last)).AsSingle();
                upper = Vector128.ConditionalSelect(Vector128.LessThan(index, last).AsSingle(), upper, Vector128<float>.One);
                return lower + ((upper - lower) * (position - Vector128.ConvertToSingle(index)));
            }

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
                Vector128<float> sample = Vector128_.Gather(ref table, middle).AsSingle();
                Vector128<int> equal = Vector128.Equals(sample, value).AsInt32() & active;
                found |= equal;
                exact = Vector128.ConditionalSelect(equal.AsSingle(), Vector128.ConvertToSingle(middle) / scale, exact);
                Vector128<int> advance = Vector128.LessThan(sample, value).AsInt32() & active;
                low = Vector128.ConditionalSelect(advance, middle + Vector128<int>.One, low);
                high = Vector128.ConditionalSelect(active & ~advance, middle - Vector128<int>.One, high);
                low = Vector128.ConditionalSelect(equal, Vector128<int>.Zero, low);
                high = Vector128.ConditionalSelect(equal, Vector128.Create(-1), high);
            }

            // Unmatched lanes now contain insertion indices. Interpolate the bracketing samples, then select endpoints
            // and exact matches. The clamped index keeps both neighbor reads valid even for endpoint and completed lanes.
            Vector128<int> insertion = Vector128.Min(Vector128.Max(low, Vector128<int>.One), last);
            Vector128<float> lowerValue = Vector128_.Gather(ref table, insertion - Vector128<int>.One).AsSingle();
            Vector128<float> upperValue = Vector128_.Gather(ref table, insertion).AsSingle();
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Vector256<float> Convert(Vector256<float> value)
        {
            // Gather copies 32-bit words unchanged. Reinterpret the float table, then restore the float lane type.
            ref int table = ref Unsafe.As<float, int>(ref MemoryMarshal.GetArrayDataReference(this.table));
            Vector256<float> scale = Vector256.Create((float)(this.table.Length - 1));
            Vector256<int> last = Vector256.Create(this.table.Length - 1);

            if (!this.inverse)
            {
                // Ordered comparisons map NaN to zero, matching Numerics.Clamp. The last cell repeats its endpoint.
                Vector256<float> bounded = Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256<float>.One), Vector256<float>.One, value);
                bounded = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(bounded, Vector256<float>.Zero), bounded, Vector256<float>.Zero);
                Vector256<float> position = bounded * scale;
                Vector256<int> index = Vector256.ConvertToInt32(position);
                Vector256<float> lower = Vector256_.Gather(ref table, index).AsSingle();
                Vector256<float> upper = Vector256_.Gather(ref table, Vector256.Min(index + Vector256<int>.One, last)).AsSingle();
                upper = Vector256.ConditionalSelect(Vector256.LessThan(index, last).AsSingle(), upper, Vector256<float>.One);
                return lower + ((upper - lower) * (position - Vector256.ConvertToSingle(index)));
            }

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
                Vector256<float> sample = Vector256_.Gather(ref table, middle).AsSingle();
                Vector256<int> equal = Vector256.Equals(sample, value).AsInt32() & active;
                found |= equal;
                exact = Vector256.ConditionalSelect(equal.AsSingle(), Vector256.ConvertToSingle(middle) / scale, exact);
                Vector256<int> advance = Vector256.LessThan(sample, value).AsInt32() & active;
                low = Vector256.ConditionalSelect(advance, middle + Vector256<int>.One, low);
                high = Vector256.ConditionalSelect(active & ~advance, middle - Vector256<int>.One, high);
                low = Vector256.ConditionalSelect(equal, Vector256<int>.Zero, low);
                high = Vector256.ConditionalSelect(equal, Vector256.Create(-1), high);
            }

            // Unmatched lanes now contain insertion indices. Interpolate the bracketing samples, then select endpoints
            // and exact matches. The clamped index keeps both neighbor reads valid even for endpoint and completed lanes.
            Vector256<int> insertion = Vector256.Min(Vector256.Max(low, Vector256<int>.One), last);
            Vector256<float> lowerValue = Vector256_.Gather(ref table, insertion - Vector256<int>.One).AsSingle();
            Vector256<float> upperValue = Vector256_.Gather(ref table, insertion).AsSingle();
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Vector512<float> Convert(Vector512<float> value)
        {
            // Gather copies 32-bit words unchanged. Reinterpret the float table, then restore the float lane type.
            ref int table = ref Unsafe.As<float, int>(ref MemoryMarshal.GetArrayDataReference(this.table));
            Vector512<float> scale = Vector512.Create((float)(this.table.Length - 1));
            Vector512<int> last = Vector512.Create(this.table.Length - 1);

            if (!this.inverse)
            {
                // Ordered comparisons map NaN to zero, matching Numerics.Clamp. The last cell repeats its endpoint.
                Vector512<float> bounded = Vector512.ConditionalSelect(Vector512.GreaterThan(value, Vector512<float>.One), Vector512<float>.One, value);
                bounded = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(bounded, Vector512<float>.Zero), bounded, Vector512<float>.Zero);
                Vector512<float> position = bounded * scale;
                Vector512<int> index = Vector512.ConvertToInt32(position);
                Vector512<float> lower = Vector512_.Gather(ref table, index).AsSingle();
                Vector512<float> upper = Vector512_.Gather(ref table, Vector512.Min(index + Vector512<int>.One, last)).AsSingle();
                upper = Vector512.ConditionalSelect(Vector512.LessThan(index, last).AsSingle(), upper, Vector512<float>.One);
                return lower + ((upper - lower) * (position - Vector512.ConvertToSingle(index)));
            }

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
                Vector512<float> sample = Vector512_.Gather(ref table, middle).AsSingle();
                Vector512<int> equal = Vector512.Equals(sample, value).AsInt32() & active;
                found |= equal;
                exact = Vector512.ConditionalSelect(equal.AsSingle(), Vector512.ConvertToSingle(middle) / scale, exact);
                Vector512<int> advance = Vector512.LessThan(sample, value).AsInt32() & active;
                low = Vector512.ConditionalSelect(advance, middle + Vector512<int>.One, low);
                high = Vector512.ConditionalSelect(active & ~advance, middle - Vector512<int>.One, high);
                low = Vector512.ConditionalSelect(equal, Vector512<int>.Zero, low);
                high = Vector512.ConditionalSelect(equal, Vector512.Create(-1), high);
            }

            // Unmatched lanes now contain insertion indices. Interpolate the bracketing samples, then select endpoints
            // and exact matches. The clamped index keeps both neighbor reads valid even for endpoint and completed lanes.
            Vector512<int> insertion = Vector512.Min(Vector512.Max(low, Vector512<int>.One), last);
            Vector512<float> lowerValue = Vector512_.Gather(ref table, insertion - Vector512<int>.One).AsSingle();
            Vector512<float> upperValue = Vector512_.Gather(ref table, insertion).AsSingle();
            Vector512<float> percent = (value - lowerValue) / (upperValue - lowerValue);
            Vector512<float> result = (Vector512.ConvertToSingle(insertion - Vector512<int>.One) / scale) + (percent * (Vector512<float>.One / scale));
            result = Vector512.ConditionalSelect(Vector512.Equals(low, Vector512<int>.Zero).AsSingle(), Vector512<float>.Zero, result);
            result = Vector512.ConditionalSelect(Vector512.GreaterThan(low, last).AsSingle(), Vector512<float>.One, result);
            return Vector512.ConditionalSelect(found.AsSingle(), exact, result);
        }
    }
}

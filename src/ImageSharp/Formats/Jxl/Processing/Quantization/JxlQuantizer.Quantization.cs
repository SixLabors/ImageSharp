// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

internal sealed partial class JxlQuantizer
{
    /// <summary>
    /// Quantizes one coefficient row with a separate zero threshold for each half.
    /// </summary>
    /// <param name="coefficients">The logical float coefficients in frequency order.</param>
    /// <param name="inverseMatrix">The corresponding inverse dequantization weights.</param>
    /// <param name="scale">The block quantizer scale including the channel matrix multiplier.</param>
    /// <param name="leftThreshold">The minimum absolute weighted magnitude retained in the first half.</param>
    /// <param name="rightThreshold">The minimum absolute weighted magnitude retained in the second half.</param>
    /// <param name="destination">The caller-owned integer coefficient row.</param>
    public static void Quantize(
        ReadOnlySpan<float> coefficients,
        ReadOnlySpan<float> inverseMatrix,
        float scale,
        float leftThreshold,
        float rightThreshold,
        Span<int> destination)
    {
        QuantizationOperator operation = new(scale);

        // An 8-column row crosses its quadrant boundary inside a 256-bit register. A 16-column row does the same at 512 bits.
        // Build the two constant threshold halves once. All input loads and the integer store cover exactly the logical row.
        if (Vector512.IsHardwareAccelerated && coefficients.Length == Vector512<float>.Count)
        {
            Vector512<float> thresholds = Vector512.Create(Vector256.Create(leftThreshold), Vector256.Create(rightThreshold));
            Vector512<float> value = operation.Invoke(Vector512.Create(coefficients), Vector512.Create(inverseMatrix), thresholds);
            Vector512_.ConvertToInt32RoundToEven(value).CopyTo(destination);
            return;
        }

        if (Vector256.IsHardwareAccelerated && coefficients.Length == Vector256<float>.Count)
        {
            Vector256<float> thresholds = Vector256.Create(Vector128.Create(leftThreshold), Vector128.Create(rightThreshold));
            Vector256<float> value = operation.Invoke(Vector256.Create(coefficients), Vector256.Create(inverseMatrix), thresholds);
            Vector256_.ConvertToInt32RoundToEven(value).CopyTo(destination);
            return;
        }

        // Larger row halves align with complete registers. Keep their threshold constant through the loop instead of comparing lane indices.
        int half = coefficients.Length / 2;
        QuantizeInterval(coefficients[..half], inverseMatrix[..half], operation, leftThreshold, destination[..half]);
        QuantizeInterval(coefficients[half..], inverseMatrix[half..], operation, rightThreshold, destination[half..]);
    }

    /// <summary>
    /// Quantizes a contiguous frequency interval whose zero threshold is constant.
    /// </summary>
    /// <param name="coefficients">The logical coefficients in one row half.</param>
    /// <param name="inverseMatrix">The matching inverse matrix weights.</param>
    /// <param name="operation">The row's prepared scale operation.</param>
    /// <param name="threshold">The interval's zero threshold.</param>
    /// <param name="destination">The matching integer output interval.</param>
    private static void QuantizeInterval(
        ReadOnlySpan<float> coefficients,
        ReadOnlySpan<float> inverseMatrix,
        QuantizationOperator operation,
        float threshold,
        Span<int> destination)
    {
        ref float input = ref MemoryMarshal.GetReference(coefficients);
        ref float weights = ref MemoryMarshal.GetReference(inverseMatrix);
        ref int output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // One lane is one coefficient. The existing conversion helpers round to nearest, with ties to even,
        // so no separate rounded-float buffer or rounding instruction is required before integer conversion.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (coefficients.Length - i).Vector512Count<float>();
            Vector512<float> thresholds = Vector512.Create(threshold);

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> value = operation.Invoke(Vector512.LoadUnsafe(ref input, (uint)i), Vector512.LoadUnsafe(ref weights, (uint)i), thresholds);
                Vector512_.ConvertToInt32RoundToEven(value).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (coefficients.Length - i).Vector256Count<float>();
            Vector256<float> thresholds = Vector256.Create(threshold);

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> value = operation.Invoke(Vector256.LoadUnsafe(ref input, (uint)i), Vector256.LoadUnsafe(ref weights, (uint)i), thresholds);
                Vector256_.ConvertToInt32RoundToEven(value).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (coefficients.Length - i).Vector128Count<float>();
            Vector128<float> thresholds = Vector128.Create(threshold);

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> value = operation.Invoke(Vector128.LoadUnsafe(ref input, (uint)i), Vector128.LoadUnsafe(ref weights, (uint)i), thresholds);
                Vector128_.ConvertToInt32RoundToEven(value).StoreUnsafe(ref output, (uint)i);
            }
        }

        // Narrow intervals and final coefficients retain the same product order, threshold comparison and midpoint rule.
        for (; i < coefficients.Length; i++)
        {
            float value = operation.Invoke(Unsafe.Add(ref input, i), Unsafe.Add(ref weights, i), threshold);
            Unsafe.Add(ref output, i) = (int)MathF.Round(value, MidpointRounding.ToEven);
        }
    }

    /// <summary>
    /// Applies inverse matrix scaling and the encoded zero threshold before integer rounding.
    /// </summary>
    private readonly struct QuantizationOperator : TensorPrimitives_.ITernaryOperator
    {
        private readonly float scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizationOperator"/> struct.
        /// </summary>
        /// <param name="scale">The block quantizer and channel multiplier.</param>
        public QuantizationOperator(float scale)
        {
            this.scale = scale;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            float value = (second * this.scale) * first;
            return MathF.Abs(value) >= third ? value : 0F;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            // Keep the native (matrix * scale) * coefficient order. Selection also clears unordered NaN comparisons.
            Vector128<float> value = (second * Vector128.Create(this.scale)) * first;
            return Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(Vector128.Abs(value), third), value, Vector128<float>.Zero);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            // Keep the native (matrix * scale) * coefficient order. Selection also clears unordered NaN comparisons.
            Vector256<float> value = (second * Vector256.Create(this.scale)) * first;
            return Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(Vector256.Abs(value), third), value, Vector256<float>.Zero);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            // Keep the native (matrix * scale) * coefficient order. Selection also clears unordered NaN comparisons.
            Vector512<float> value = (second * Vector512.Create(this.scale)) * first;
            return Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(Vector512.Abs(value), third), value, Vector512<float>.Zero);
        }
    }
}

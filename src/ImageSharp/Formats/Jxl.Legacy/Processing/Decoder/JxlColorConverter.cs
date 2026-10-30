// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Converts reconstructed JPEG XL component rows to RGB with operation-wide prepared coefficients.
/// </summary>
internal abstract partial class JxlColorConverter
{
    /// <summary>
    /// Creates the converter for centered Cb, biased Y and centered Cr components.
    /// </summary>
    /// <returns>The prepared BT.601 converter.</returns>
    public static JxlColorConverter CreateYCbCr() => new Converter<YCbCrOperator>(new YCbCrOperator());

    /// <summary>
    /// Creates the converter from XYB to linear RGB.
    /// </summary>
    /// <param name="transform">The selected inverse absorbance transform and original reference intensity.</param>
    /// <returns>The prepared XYB converter.</returns>
    public static JxlColorConverter CreateXyb(XybTransform transform) => new Converter<XybOperator>(new XybOperator(transform));

    /// <summary>
    /// Converts three equally sized logical rows without rounding or packing the RGB results.
    /// </summary>
    /// <param name="source0">The first source component.</param>
    /// <param name="source1">The corresponding second component.</param>
    /// <param name="source2">The corresponding third component.</param>
    /// <param name="red">The equally sized red destination.</param>
    /// <param name="green">The equally sized green destination.</param>
    /// <param name="blue">The equally sized blue destination.</param>
    /// <remarks>Corresponding source and destination rows can have the same start. Offset overlap is not supported.</remarks>
    public abstract void Convert(
        ReadOnlySpan<float> source0,
        ReadOnlySpan<float> source1,
        ReadOnlySpan<float> source2,
        Span<float> red,
        Span<float> green,
        Span<float> blue);

    /// <summary>
    /// Defines the same three-component arithmetic for one color and every supported SIMD width.
    /// </summary>
    private interface IColorOperator
    {
        /// <summary>
        /// Replaces 1 independent source colors with RGB components.
        /// </summary>
        /// <param name="component0">The first component, replaced by red.</param>
        /// <param name="component1">The second component, replaced by green.</param>
        /// <param name="component2">The third component, replaced by blue.</param>
        void Invoke(ref float component0, ref float component1, ref float component2);

        /// <summary>
        /// Replaces 4 independent source colors with RGB components.
        /// </summary>
        /// <param name="component0">The first component, replaced by red.</param>
        /// <param name="component1">The second component, replaced by green.</param>
        /// <param name="component2">The third component, replaced by blue.</param>
        void Invoke(ref Vector128<float> component0, ref Vector128<float> component1, ref Vector128<float> component2);

        /// <summary>
        /// Replaces 8 independent source colors with RGB components.
        /// </summary>
        /// <param name="component0">The first component, replaced by red.</param>
        /// <param name="component1">The second component, replaced by green.</param>
        /// <param name="component2">The third component, replaced by blue.</param>
        void Invoke(ref Vector256<float> component0, ref Vector256<float> component1, ref Vector256<float> component2);

        /// <summary>
        /// Replaces 16 independent source colors with RGB components.
        /// </summary>
        /// <param name="component0">The first component, replaced by red.</param>
        /// <param name="component1">The second component, replaced by green.</param>
        /// <param name="component2">The third component, replaced by blue.</param>
        void Invoke(ref Vector512<float> component0, ref Vector512<float> component1, ref Vector512<float> component2);

    }

    /// <summary>
    /// Traverses the logical rows with the operator fixed by the closed generic type.
    /// </summary>
    /// <typeparam name="TOperator">The prepared, allocation-free component arithmetic.</typeparam>
    private sealed class Converter<TOperator> : JxlColorConverter
        where TOperator : struct, IColorOperator
    {
        /// <summary>
        /// The immutable coefficients shared by every row and SIMD width.
        /// </summary>
        private readonly TOperator operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="Converter{TOperator}"/> class.
        /// </summary>
        /// <param name="operation">The operation prepared before processing the frame.</param>
        public Converter(TOperator operation) => this.operation = operation;

        /// <inheritdoc/>
        public override void Convert(
            ReadOnlySpan<float> source0,
            ReadOnlySpan<float> source1,
            ReadOnlySpan<float> source2,
            Span<float> red,
            Span<float> green,
            Span<float> blue)
        {
            ref float input0 = ref MemoryMarshal.GetReference(source0);
            ref float input1 = ref MemoryMarshal.GetReference(source1);
            ref float input2 = ref MemoryMarshal.GetReference(source2);
            ref float output0 = ref MemoryMarshal.GetReference(red);
            ref float output1 = ref MemoryMarshal.GetReference(green);
            ref float output2 = ref MemoryMarshal.GetReference(blue);
            int x = 0;

            // Each register contains one component from consecutive pixels. Read all three inputs before any store.
            // Descending widths share one offset. Complete registers and the scalar tail never access row padding.
            // The constrained call uses the prepared value type directly; no interface object or per-pixel dispatch is needed.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= source0.Length - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    Vector512<float> value0 = Vector512.LoadUnsafe(ref input0, (uint)x);
                    Vector512<float> value1 = Vector512.LoadUnsafe(ref input1, (uint)x);
                    Vector512<float> value2 = Vector512.LoadUnsafe(ref input2, (uint)x);
                    this.operation.Invoke(ref value0, ref value1, ref value2);
                    value0.StoreUnsafe(ref output0, (uint)x);
                    value1.StoreUnsafe(ref output1, (uint)x);
                    value2.StoreUnsafe(ref output2, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= source0.Length - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    Vector256<float> value0 = Vector256.LoadUnsafe(ref input0, (uint)x);
                    Vector256<float> value1 = Vector256.LoadUnsafe(ref input1, (uint)x);
                    Vector256<float> value2 = Vector256.LoadUnsafe(ref input2, (uint)x);
                    this.operation.Invoke(ref value0, ref value1, ref value2);
                    value0.StoreUnsafe(ref output0, (uint)x);
                    value1.StoreUnsafe(ref output1, (uint)x);
                    value2.StoreUnsafe(ref output2, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= source0.Length - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    Vector128<float> value0 = Vector128.LoadUnsafe(ref input0, (uint)x);
                    Vector128<float> value1 = Vector128.LoadUnsafe(ref input1, (uint)x);
                    Vector128<float> value2 = Vector128.LoadUnsafe(ref input2, (uint)x);
                    this.operation.Invoke(ref value0, ref value1, ref value2);
                    value0.StoreUnsafe(ref output0, (uint)x);
                    value1.StoreUnsafe(ref output1, (uint)x);
                    value2.StoreUnsafe(ref output2, (uint)x);
                }
            }

            for (; x < source0.Length; x++)
            {
                float value0 = Unsafe.Add(ref input0, x);
                float value1 = Unsafe.Add(ref input1, x);
                float value2 = Unsafe.Add(ref input2, x);
                this.operation.Invoke(ref value0, ref value1, ref value2);
                Unsafe.Add(ref output0, x) = value0;
                Unsafe.Add(ref output1, x) = value1;
                Unsafe.Add(ref output2, x) = value2;
            }
        }
    }
}

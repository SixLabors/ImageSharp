// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Defines component arithmetic independently of row traversal and ICC transforms.
    /// </summary>
    /// <remarks>
    /// Each overload applies the same equations to independent pixels. Prepared struct fields hold
    /// coefficients selected before traversal. The constrained calls do not box the selected operator.
    /// </remarks>
    public interface IJxlColorConverterOperator
    {
        /// <summary>
        /// Gets the number of source device components.
        /// </summary>
        public static abstract int ComponentCount { get; }

        /// <summary>
        /// Restores the selected output components for one pixel.
        /// </summary>
        /// <param name="c0">The first component, replaced by red or the first ICC device component.</param>
        /// <param name="c1">The second component, replaced by green or the second ICC device component.</param>
        /// <param name="c2">The third component, replaced by blue or the third ICC device component.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3);

        /// <summary>
        /// Restores the selected output components for four pixels.
        /// </summary>
        /// <param name="c0">The first component, replaced by red or the first ICC device component.</param>
        /// <param name="c1">The second component, replaced by green or the second ICC device component.</param>
        /// <param name="c2">The third component, replaced by blue or the third ICC device component.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3);

        /// <summary>
        /// Restores the selected output components for eight pixels.
        /// </summary>
        /// <param name="c0">The first component, replaced by red or the first ICC device component.</param>
        /// <param name="c1">The second component, replaced by green or the second ICC device component.</param>
        /// <param name="c2">The third component, replaced by blue or the third ICC device component.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3);

        /// <summary>
        /// Restores the selected output components for sixteen pixels.
        /// </summary>
        /// <param name="c0">The first component, replaced by red or the first ICC device component.</param>
        /// <param name="c1">The second component, replaced by green or the second ICC device component.</param>
        /// <param name="c2">The third component, replaced by blue or the third ICC device component.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3);
    }

    /// <summary>
    /// Applies a three-component color operation to distinct component planes in place.
    /// </summary>
    /// <typeparam name="TOperator">The prepared three-component operation selected for the frame.</typeparam>
    /// <param name="component0">The first mutable component row.</param>
    /// <param name="component1">The second mutable component row, with the same validated length.</param>
    /// <param name="component2">The third mutable component row, with the same validated length.</param>
    /// <param name="operation">The shared scalar and SIMD component equations.</param>
    /// <remarks>
    /// Frame reconstruction owns these rows. This method changes only their three color components;
    /// it does not pack pixels, apply a transfer curve or process extra channels.
    /// Three-component operators leave their fourth argument unused.
    /// </remarks>
    public static void ConvertInPlace<TOperator>(Span<float> component0, Span<float> component1, Span<float> component2, TOperator operation)
        where TOperator : struct, IJxlColorConverterOperator
    {
        int count = component0.Length;
        ref float first = ref MemoryMarshal.GetReference(component0);
        ref float second = ref MemoryMarshal.GetReference(component1);
        ref float third = ref MemoryMarshal.GetReference(component2);
        int i = 0;

        // Each register holds one component from consecutive pixels. The owner supplies equally sized,
        // distinct rows. A complete-block count bounds every load and store within those rows.
        // Load all three components before any store, then retain the results in the same planar layout.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = (count - i).Vector512Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
            {
                Vector512<float> x = Vector512.LoadUnsafe(ref first, (nuint)i);
                Vector512<float> y = Vector512.LoadUnsafe(ref second, (nuint)i);
                Vector512<float> z = Vector512.LoadUnsafe(ref third, (nuint)i);
                Vector512<float> unused = default;

                operation.Invoke(ref x, ref y, ref z, ref unused);

                x.StoreUnsafe(ref first, (nuint)i);
                y.StoreUnsafe(ref second, (nuint)i);
                z.StoreUnsafe(ref third, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = (count - i).Vector256Count<float>();

            // Continue at the first unprocessed pixel. Only complete smaller blocks reach these loads.
            for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
            {
                Vector256<float> x = Vector256.LoadUnsafe(ref first, (nuint)i);
                Vector256<float> y = Vector256.LoadUnsafe(ref second, (nuint)i);
                Vector256<float> z = Vector256.LoadUnsafe(ref third, (nuint)i);
                Vector256<float> unused = default;

                operation.Invoke(ref x, ref y, ref z, ref unused);

                x.StoreUnsafe(ref first, (nuint)i);
                y.StoreUnsafe(ref second, (nuint)i);
                z.StoreUnsafe(ref third, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = (count - i).Vector128Count<float>();

            // The last complete SIMD blocks use the same planar lanes and component equations.
            for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
            {
                Vector128<float> x = Vector128.LoadUnsafe(ref first, (nuint)i);
                Vector128<float> y = Vector128.LoadUnsafe(ref second, (nuint)i);
                Vector128<float> z = Vector128.LoadUnsafe(ref third, (nuint)i);
                Vector128<float> unused = default;

                operation.Invoke(ref x, ref y, ref z, ref unused);

                x.StoreUnsafe(ref first, (nuint)i);
                y.StoreUnsafe(ref second, (nuint)i);
                z.StoreUnsafe(ref third, (nuint)i);
            }
        }

        // Fewer than four pixels remain, or hardware intrinsics are disabled. Index each row directly;
        // the scalar operator applies the same equations without reading or writing beyond the row.
        for (; i < count; i++)
        {
            float x = component0[i];
            float y = component1[i];
            float z = component2[i];
            float unused = 0F;

            operation.Invoke(ref x, ref y, ref z, ref unused);

            component0[i] = x;
            component1[i] = y;
            component2[i] = z;
        }
    }

    /// <summary>
    /// Traverses source planes with one prepared component operator.
    /// </summary>
    /// <typeparam name="TOperator">The component operation selected for this frame.</typeparam>
    private sealed partial class JxlColorConverter<TOperator> : JxlColorConverterBase
        where TOperator : struct, IJxlColorConverterOperator
    {
        private readonly TOperator operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for direct or ICC component conversion.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic, separate from the final output conversion.</param>
        /// <param name="isLinearRgb">True when the component operation produces linear sRGB that needs compression.</param>
        public JxlColorConverter(TOperator operation, bool isLinearRgb)
            : base(isLinearRgb)
        {
            this.operation = operation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for a compact RGB description.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic.</param>
        /// <param name="sourceWorkingSpace">The source transfer curve, primaries and reference white.</param>
        public JxlColorConverter(TOperator operation, RgbWorkingSpace sourceWorkingSpace)
            : base(sourceWorkingSpace)
        {
            this.operation = operation;
        }

        /// <inheritdoc/>
        public override void ConvertComponents<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<Vector4> destination,
            in TSampleOperator sampleOperator)
        {
            // Copy the prepared operation once. Calling a constrained instance method on its readonly
            // field would copy the struct for every SIMD block, including its matrix and bias fields.
            TOperator operation = this.operation;
            TSampleOperator sampleOperation = sampleOperator;

            // Reconstructed planes stay fixed during conversion. Retain their spans locally so stores
            // and constrained calls cannot force the JIT to reload the borrowed descriptor in each block.
            ReadOnlySpan<TSample> component0 = source.Component0;
            ReadOnlySpan<TSample> component1 = source.Component1;
            ReadOnlySpan<TSample> component2 = source.Component2;
            int count = component0.Length;
            ref float black = ref MemoryMarshal.GetReference(source.Black);
            ref float alpha = ref MemoryMarshal.GetReference(source.Alpha);
            bool hasAlpha = !source.Alpha.IsEmpty;
            ref float target = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int i = 0;

            // Each register holds one component from consecutive pixels. Convert in registers, then interleave
            // directly into the destination row. Counts use float output lanes. Short carriers fill only the low half
            // of each input register, so the shared loads read exactly one sample per output pixel.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector512Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> x = sampleOperation.Invoke(JxlSampleConverter.Load512(component0, i));
                    Vector512<float> y = sampleOperation.Invoke(JxlSampleConverter.Load512(component1, i));
                    Vector512<float> z = sampleOperation.Invoke(JxlSampleConverter.Load512(component2, i));
                    Vector512<float> k = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    Vector512<float> opacity = hasAlpha ? Vector512.LoadUnsafe(ref alpha, (nuint)i) : Vector512<float>.One;
                    SimdUtils.Interleave4(x, y, z, opacity, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector256Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x = sampleOperation.Invoke(JxlSampleConverter.Load256(component0, i));
                    Vector256<float> y = sampleOperation.Invoke(JxlSampleConverter.Load256(component1, i));
                    Vector256<float> z = sampleOperation.Invoke(JxlSampleConverter.Load256(component2, i));
                    Vector256<float> k = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    Vector256<float> opacity = hasAlpha ? Vector256.LoadUnsafe(ref alpha, (nuint)i) : Vector256<float>.One;
                    SimdUtils.Interleave4(x, y, z, opacity, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector128Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x = sampleOperation.Invoke(JxlSampleConverter.Load128(component0, i));
                    Vector128<float> y = sampleOperation.Invoke(JxlSampleConverter.Load128(component1, i));
                    Vector128<float> z = sampleOperation.Invoke(JxlSampleConverter.Load128(component2, i));
                    Vector128<float> k = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    Vector128<float> opacity = hasAlpha ? Vector128.LoadUnsafe(ref alpha, (nuint)i) : Vector128<float>.One;
                    SimdUtils.Interleave4(x, y, z, opacity, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            // The scalar remainder uses the same operator and sample normalization as every vector width.
            for (; i < count; i++)
            {
                float x = sampleOperation.Invoke(component0[i]);
                float y = sampleOperation.Invoke(component1[i]);
                float z = sampleOperation.Invoke(component2[i]);
                float k = TOperator.ComponentCount == 4 ? source.Black[i] : 0F;
                operation.Invoke(ref x, ref y, ref z, ref k);
                destination[i] = new Vector4(x, y, z, hasAlpha ? source.Alpha[i] : 1F);
            }
        }
    }
}

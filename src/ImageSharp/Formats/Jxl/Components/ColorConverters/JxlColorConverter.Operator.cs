// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

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
        /// Gets the number of color components handled by the operation.
        /// </summary>
        public static abstract int ComponentCount { get; }

        /// <summary>
        /// Applies the selected component operation for one pixel.
        /// </summary>
        /// <param name="c0">The first color component, updated in place.</param>
        /// <param name="c1">The second color component, updated in place.</param>
        /// <param name="c2">The third color component, updated in place.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3);

        /// <summary>
        /// Applies the selected component operation for four pixels.
        /// </summary>
        /// <param name="c0">The first color component, updated in place.</param>
        /// <param name="c1">The second color component, updated in place.</param>
        /// <param name="c2">The third color component, updated in place.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3);

        /// <summary>
        /// Applies the selected component operation for eight pixels.
        /// </summary>
        /// <param name="c0">The first color component, updated in place.</param>
        /// <param name="c1">The second color component, updated in place.</param>
        /// <param name="c2">The third color component, updated in place.</param>
        /// <param name="c3">The black component, replaced by ICC ink amount for CMYK. Three-component models leave it unused.</param>
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3);

        /// <summary>
        /// Applies the selected component operation for sixteen pixels.
        /// </summary>
        /// <param name="c0">The first color component, updated in place.</param>
        /// <param name="c1">The second color component, updated in place.</param>
        /// <param name="c2">The third color component, updated in place.</param>
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
        /// The prepared grayscale input stage for coding XYB, or null when the original RGB components are retained.
        /// </summary>
        private readonly JxlColorConverter<LuminanceOperator>? luminanceInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for direct or ICC component conversion.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic, separate from the final output conversion.</param>
        public JxlColorConverter(TOperator operation)
        {
            this.operation = operation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for conversion between linear components and working RGB.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic.</param>
        /// <param name="linearOutputWorkingSpace">The working space whose transfer curve connects encoded RGB and linear components.</param>
        /// <param name="luminanceInput">Whether encoding first replaces working RGB with its luminance component.</param>
        public JxlColorConverter(TOperator operation, RgbWorkingSpace linearOutputWorkingSpace, bool luminanceInput = false)
            : base(linearOutputWorkingSpace)
        {
            this.operation = operation;

            if (luminanceInput)
            {
                this.luminanceInput = new(default(LuminanceOperator));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for compact RGB conversion.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic.</param>
        /// <param name="sourceWorkingSpace">The source transfer curve, primaries and reference white.</param>
        /// <param name="targetWorkingSpace">The required output transfer curve, primaries and reference white.</param>
        public JxlColorConverter(TOperator operation, RgbWorkingSpace sourceWorkingSpace, RgbWorkingSpace targetWorkingSpace)
            : base(sourceWorkingSpace, targetWorkingSpace)
        {
            this.operation = operation;
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(
            Span<Vector4> source,
            Span<float> component0,
            Span<float> component1,
            Span<float> component2,
            Span<float> black,
            Span<float> alpha,
            bool associateAlpha,
            ColorProfileConverter? profileConverter,
            Span<float> scratch)
        {
            // The decoder unassociates working RGB after its color conversion. Apply the inverse order:
            // associate working RGB before the profile or coding transform. Only this borrowed row changes; W remains alpha.
            if (associateAlpha)
            {
                Numerics.Premultiply(source);
            }

            if (profileConverter is not null)
            {
                this.ConvertFromIcc(source, component0, component1, component2, black, alpha, profileConverter, scratch);
                return;
            }

            if (this.luminanceInput is not null)
            {
                // ColorType.Luminance selects the existing encoded BT.709 luminance conversion.
                // Run it before curve expansion, as for ordinary grayscale input. Its first output row
                // is reused as three equal RGB planes; shared SIMD interleaving retains the alpha lane.
                this.luminanceInput.ConvertFromRgb(source, component0, [], [], [], alpha, false, null, []);
                SimdUtils.InterleaveFloatPlanes(component0, component0, component0, alpha, source);
            }

            // Original compact components use the inverse of the decoder's prepared RGB conversion.
            // The shared worker owns the transfer curves, adaptation and matrices. It retains alpha.
            if (this.rgbConversion.HasValue)
            {
                this.rgbConversion.Value.Invoke(source);
            }

            // Coding XYB consumes linear sRGB. The existing working-space expansion retains the alpha lane.
            this.linearWorkingSpace?.Expand(source);
            TOperator operation = this.operation;
            bool hasAlpha = !alpha.IsEmpty;
            ref float first = ref MemoryMarshal.GetReference(component0);
            ref float second = ref MemoryMarshal.GetReference(component1);
            ref float third = ref MemoryMarshal.GetReference(component2);
            ref float opacity = ref MemoryMarshal.GetReference(alpha);
            int i = 0;

            // Deinterleave complete RGBA blocks in registers. The operator transforms only RGB;
            // W goes directly to the separate alpha row. Luminance operators store only the first result.
            // Every row is equally wide and owned by the encoder. Smaller blocks and scalar tails never overread it.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint count = (source.Length - i).Vector512Count<float>();

                for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
                {
                    SimdUtils.Deinterleave4(source, i, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b, out Vector512<float> a);
                    Vector512<float> unused = default;
                    operation.Invoke(ref r, ref g, ref b, ref unused);
                    r.StoreUnsafe(ref first, (nuint)i);

                    if (TOperator.ComponentCount != 1)
                    {
                        g.StoreUnsafe(ref second, (nuint)i);
                        b.StoreUnsafe(ref third, (nuint)i);
                    }

                    if (hasAlpha)
                    {
                        a.StoreUnsafe(ref opacity, (nuint)i);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint count = (source.Length - i).Vector256Count<float>();

                for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
                {
                    SimdUtils.Deinterleave4(source, i, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b, out Vector256<float> a);
                    Vector256<float> unused = default;
                    operation.Invoke(ref r, ref g, ref b, ref unused);
                    r.StoreUnsafe(ref first, (nuint)i);

                    if (TOperator.ComponentCount != 1)
                    {
                        g.StoreUnsafe(ref second, (nuint)i);
                        b.StoreUnsafe(ref third, (nuint)i);
                    }

                    if (hasAlpha)
                    {
                        a.StoreUnsafe(ref opacity, (nuint)i);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint count = (source.Length - i).Vector128Count<float>();

                for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
                {
                    SimdUtils.Deinterleave4(source, i, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b, out Vector128<float> a);
                    Vector128<float> unused = default;
                    operation.Invoke(ref r, ref g, ref b, ref unused);
                    r.StoreUnsafe(ref first, (nuint)i);

                    if (TOperator.ComponentCount != 1)
                    {
                        g.StoreUnsafe(ref second, (nuint)i);
                        b.StoreUnsafe(ref third, (nuint)i);
                    }

                    if (hasAlpha)
                    {
                        a.StoreUnsafe(ref opacity, (nuint)i);
                    }
                }
            }

            // The scalar tail uses the same prepared operation and preserves the original alpha bits.
            for (; i < source.Length; i++)
            {
                Vector4 value = source[i];
                float r = value.X;
                float g = value.Y;
                float b = value.Z;
                float unused = 0F;
                operation.Invoke(ref r, ref g, ref b, ref unused);
                component0[i] = r;

                if (TOperator.ComponentCount != 1)
                {
                    component1[i] = g;
                    component2[i] = b;
                }

                if (hasAlpha)
                {
                    alpha[i] = value.W;
                }
            }
        }

        /// <inheritdoc/>
        public override void ConvertComponents<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<Vector4> destination,
            IccColorSpaceType profileSpace,
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
            bool gray = profileSpace == IccColorSpaceType.Gray;
            bool writeBlack = TOperator.ComponentCount == 4 && profileSpace == IccColorSpaceType.Cmyk;
            ref float target = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int i = 0;

            // Each register holds one component from consecutive pixels. Convert in registers, then interleave
            // directly into the destination row. Counts use float output lanes. Short carriers fill only the low half
            // of each input register, so the shared loads read exactly one sample per output pixel.
            // Gray profiles read the restored luminance. CMYK profiles read black from W and keep alpha in its own row.
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
                    Vector512<float> w = writeBlack ? k : hasAlpha ? Vector512.LoadUnsafe(ref alpha, (nuint)i) : Vector512<float>.One;
                    SimdUtils.Interleave4(x, gray ? x : y, gray ? x : z, w, ref Unsafe.Add(ref target, (nuint)i * 4));
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
                    Vector256<float> w = writeBlack ? k : hasAlpha ? Vector256.LoadUnsafe(ref alpha, (nuint)i) : Vector256<float>.One;
                    SimdUtils.Interleave4(x, gray ? x : y, gray ? x : z, w, ref Unsafe.Add(ref target, (nuint)i * 4));
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
                    Vector128<float> w = writeBlack ? k : hasAlpha ? Vector128.LoadUnsafe(ref alpha, (nuint)i) : Vector128<float>.One;
                    SimdUtils.Interleave4(x, gray ? x : y, gray ? x : z, w, ref Unsafe.Add(ref target, (nuint)i * 4));
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
                float w = writeBlack ? k : hasAlpha ? source.Alpha[i] : 1F;
                destination[i] = new Vector4(x, gray ? x : y, gray ? x : z, w);
            }
        }
    }
}

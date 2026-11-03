// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
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
    /// Traverses source planes with one prepared component operator.
    /// </summary>
    /// <typeparam name="TOperator">The component operation selected for this frame.</typeparam>
    private sealed partial class JxlColorConverter<TOperator> : JxlColorConverterBase
        where TOperator : struct, IJxlColorConverterOperator
    {
        // The operation is prepared once and never mutated. Keep the generic receiver writable so
        // constrained instance calls do not require a defensive copy of its matrix and bias fields.
        private TOperator operation;

        // Coding XYB produces linear sRGB. The compander owns the following transfer and result bounds.
        private readonly bool isLinearRgb;
        private readonly ColorProfileConverter.RgbConversion? rgbConversion;

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for direct or ICC component conversion.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic. ICC conversion is supplied only to the ICC overload.</param>
        /// <param name="isLinearRgb">True when the component operation produces linear sRGB that needs compression.</param>
        public JxlColorConverter(TOperator operation, bool isLinearRgb)
        {
            this.operation = operation;
            this.isLinearRgb = isLinearRgb;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class for a compact RGB description.
        /// </summary>
        /// <param name="operation">The prepared component arithmetic.</param>
        /// <param name="sourceWorkingSpace">The source transfer curve, primaries and reference white.</param>
        public JxlColorConverter(TOperator operation, RgbWorkingSpace sourceWorkingSpace)
        {
            this.operation = operation;
            Matrix4x4 adaptation = KnownChromaticAdaptationMatrices.Bradford;
            Matrix4x4.Invert(adaptation, out Matrix4x4 inverse);
            RgbWorkingSpace target = KnownRgbWorkingSpaces.SRgb;
            this.rgbConversion = new ColorProfileConverter.RgbConversion(
                sourceWorkingSpace,
                target,
                (sourceWorkingSpace.WhitePoint, target.WhitePoint),
                adaptation,
                inverse);
        }

        /// <inheritdoc/>
        public override void ConvertToRgb<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<Vector4> destination,
            in TSampleOperator sampleOperator)
        {
            int count = source.Component0.Length;
            ref TSample c0 = ref MemoryMarshal.GetReference(source.Component0);
            ref TSample c1 = ref MemoryMarshal.GetReference(source.Component1);
            ref TSample c2 = ref MemoryMarshal.GetReference(source.Component2);
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
                    Vector512<float> x = sampleOperator.Invoke(JxlSampleConverter.Load512(source.Component0, i));
                    Vector512<float> y = sampleOperator.Invoke(JxlSampleConverter.Load512(source.Component1, i));
                    Vector512<float> z = sampleOperator.Invoke(JxlSampleConverter.Load512(source.Component2, i));
                    Vector512<float> k = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref black, (nuint)i) : default;
                    this.operation.Invoke(ref x, ref y, ref z, ref k);
                    Vector512<float> opacity = hasAlpha ? Vector512.LoadUnsafe(ref alpha, (nuint)i) : Vector512<float>.One;
                    SimdUtils.Interleave4(x, y, z, opacity, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector256Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x = sampleOperator.Invoke(JxlSampleConverter.Load256(source.Component0, i));
                    Vector256<float> y = sampleOperator.Invoke(JxlSampleConverter.Load256(source.Component1, i));
                    Vector256<float> z = sampleOperator.Invoke(JxlSampleConverter.Load256(source.Component2, i));
                    Vector256<float> k = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref black, (nuint)i) : default;
                    this.operation.Invoke(ref x, ref y, ref z, ref k);
                    Vector256<float> opacity = hasAlpha ? Vector256.LoadUnsafe(ref alpha, (nuint)i) : Vector256<float>.One;
                    SimdUtils.Interleave4(x, y, z, opacity, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector128Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x = sampleOperator.Invoke(JxlSampleConverter.Load128(source.Component0, i));
                    Vector128<float> y = sampleOperator.Invoke(JxlSampleConverter.Load128(source.Component1, i));
                    Vector128<float> z = sampleOperator.Invoke(JxlSampleConverter.Load128(source.Component2, i));
                    Vector128<float> k = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref black, (nuint)i) : default;
                    this.operation.Invoke(ref x, ref y, ref z, ref k);
                    Vector128<float> opacity = hasAlpha ? Vector128.LoadUnsafe(ref alpha, (nuint)i) : Vector128<float>.One;
                    SimdUtils.Interleave4(x, y, z, opacity, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            // The scalar remainder uses the same operator and sample normalization as every vector width.
            for (; i < count; i++)
            {
                float x = sampleOperator.Invoke(Unsafe.Add(ref c0, i));
                float y = sampleOperator.Invoke(Unsafe.Add(ref c1, i));
                float z = sampleOperator.Invoke(Unsafe.Add(ref c2, i));
                float k = TOperator.ComponentCount == 4 ? Unsafe.Add(ref black, i) : 0F;
                this.operation.Invoke(ref x, ref y, ref z, ref k);
                destination[i] = new Vector4(x, y, z, hasAlpha ? Unsafe.Add(ref alpha, i) : 1F);
            }

            if (this.isLinearRgb)
            {
                SRgbCompanding.Compress(destination);
            }
            else if (this.rgbConversion.HasValue)
            {
                // Reuse the converter's prepared RGB arithmetic without constructing a general converter.
                // Transfer curves, including PQ and HLG, remain owned by their working-space implementations.
                this.rgbConversion.Value.Invoke(destination);
            }
        }
    }
}

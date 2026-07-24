// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Defines the color-model-specific arithmetic used by <see cref="JpegColorConverter{TOperator}"/>.
    /// </summary>
    /// <remarks>
    /// Each overload describes the same lane-wise transform. The generic traversal selects the widest
    /// available overload and the JIT resolves these static interface calls for each closed converter type.
    /// </remarks>
    internal interface IJpegColorConverterOperator
    {
        /// <summary>
        /// Gets the JPEG color space handled by the operator.
        /// </summary>
        public static abstract JpegColorSpace ColorSpace { get; }

        /// <summary>
        /// Gets the number of component planes used by the color space.
        /// </summary>
        public static abstract int ComponentCount { get; }

        /// <summary>
        /// Converts one JPEG sample to normalized RGB.
        /// </summary>
        /// <param name="c0">The first component, replaced by red.</param>
        /// <param name="c1">The second component, replaced by green.</param>
        /// <param name="c2">The third component, replaced by blue.</param>
        /// <param name="c3">The fourth component, or zero for a three-component color space.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/>.</param>
        public static abstract void ConvertToRgb(
            ref float c0,
            ref float c1,
            ref float c2,
            float c3,
            float maximumValue,
            float halfValue,
            float scale);

        /// <summary>
        /// Converts four JPEG samples to normalized RGB.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced by red.</param>
        /// <param name="c1">The second component lanes, replaced by green.</param>
        /// <param name="c2">The third component lanes, replaced by blue.</param>
        /// <param name="c3">The fourth component lanes, or zero for a three-component color space.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/> in every lane.</param>
        public static abstract void ConvertToRgb(
            ref Vector128<float> c0,
            ref Vector128<float> c1,
            ref Vector128<float> c2,
            Vector128<float> c3,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale);

        /// <summary>
        /// Converts eight JPEG samples to normalized RGB.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced by red.</param>
        /// <param name="c1">The second component lanes, replaced by green.</param>
        /// <param name="c2">The third component lanes, replaced by blue.</param>
        /// <param name="c3">The fourth component lanes, or zero for a three-component color space.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/> in every lane.</param>
        public static abstract void ConvertToRgb(
            ref Vector256<float> c0,
            ref Vector256<float> c1,
            ref Vector256<float> c2,
            Vector256<float> c3,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale);

        /// <summary>
        /// Converts sixteen JPEG samples to normalized RGB.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced by red.</param>
        /// <param name="c1">The second component lanes, replaced by green.</param>
        /// <param name="c2">The third component lanes, replaced by blue.</param>
        /// <param name="c3">The fourth component lanes, or zero for a three-component color space.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/> in every lane.</param>
        public static abstract void ConvertToRgb(
            ref Vector512<float> c0,
            ref Vector512<float> c1,
            ref Vector512<float> c2,
            Vector512<float> c3,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale);

        /// <summary>
        /// Converts one RGB sample to JPEG components.
        /// </summary>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/>.</param>
        /// <param name="c0">The first converted component.</param>
        /// <param name="c1">The second converted component.</param>
        /// <param name="c2">The third converted component.</param>
        /// <param name="c3">The fourth converted component, if used.</param>
        public static abstract void ConvertFromRgb(
            float r,
            float g,
            float b,
            float maximumValue,
            float halfValue,
            float scale,
            out float c0,
            out float c1,
            out float c2,
            out float c3);

        /// <summary>
        /// Converts four RGB samples to JPEG components.
        /// </summary>
        /// <param name="r">The red lanes.</param>
        /// <param name="g">The green lanes.</param>
        /// <param name="b">The blue lanes.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/> in every lane.</param>
        /// <param name="c0">The first converted component lanes.</param>
        /// <param name="c1">The second converted component lanes.</param>
        /// <param name="c2">The third converted component lanes.</param>
        /// <param name="c3">The fourth converted component lanes, if used.</param>
        public static abstract void ConvertFromRgb(
            Vector128<float> r,
            Vector128<float> g,
            Vector128<float> b,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            out Vector128<float> c0,
            out Vector128<float> c1,
            out Vector128<float> c2,
            out Vector128<float> c3);

        /// <summary>
        /// Converts eight RGB samples to JPEG components.
        /// </summary>
        /// <param name="r">The red lanes.</param>
        /// <param name="g">The green lanes.</param>
        /// <param name="b">The blue lanes.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/> in every lane.</param>
        /// <param name="c0">The first converted component lanes.</param>
        /// <param name="c1">The second converted component lanes.</param>
        /// <param name="c2">The third converted component lanes.</param>
        /// <param name="c3">The fourth converted component lanes, if used.</param>
        public static abstract void ConvertFromRgb(
            Vector256<float> r,
            Vector256<float> g,
            Vector256<float> b,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            out Vector256<float> c0,
            out Vector256<float> c1,
            out Vector256<float> c2,
            out Vector256<float> c3);

        /// <summary>
        /// Converts sixteen RGB samples to JPEG components.
        /// </summary>
        /// <param name="r">The red lanes.</param>
        /// <param name="g">The green lanes.</param>
        /// <param name="b">The blue lanes.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/> in every lane.</param>
        /// <param name="c0">The first converted component lanes.</param>
        /// <param name="c1">The second converted component lanes.</param>
        /// <param name="c2">The third converted component lanes.</param>
        /// <param name="c3">The fourth converted component lanes, if used.</param>
        public static abstract void ConvertFromRgb(
            Vector512<float> r,
            Vector512<float> g,
            Vector512<float> b,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            out Vector512<float> c0,
            out Vector512<float> c1,
            out Vector512<float> c2,
            out Vector512<float> c3);

        /// <summary>
        /// Converts JPEG component values to RGB using the supplied ICC profile.
        /// </summary>
        /// <param name="configuration">The configuration used to allocate temporary storage.</param>
        /// <param name="profile">The source ICC profile.</param>
        /// <param name="values">The component values to convert.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        public static abstract void ConvertToRgbInPlaceWithIcc(
            Configuration configuration,
            IccProfile profile,
            in ComponentValues values,
            float maximumValue);
    }

    /// <summary>
    /// Converts a JPEG color model using a single operator-driven traversal for all SIMD widths.
    /// </summary>
    /// <typeparam name="TOperator">The color-model-specific arithmetic.</typeparam>
    internal sealed class JpegColorConverter<TOperator> : JpegColorConverterBase
        where TOperator : struct, IJpegColorConverterOperator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JpegColorConverter{TOperator}"/> class.
        /// </summary>
        /// <param name="precision">The precision in bits.</param>
        public JpegColorConverter(int precision)
            : base(TOperator.ColorSpace, precision)
        {
        }

        /// <inheritdoc/>
        public override bool IsAvailable => true;

        /// <inheritdoc/>
        public override int ElementsPerBatch
            => Vector512.IsHardwareAccelerated
                ? Vector512<float>.Count
                : Vector256.IsHardwareAccelerated
                    ? Vector256<float>.Count
                    : Vector128.IsHardwareAccelerated
                        ? Vector128<float>.Count
                        : 1;

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            // JPEG component processors own equally sized planar buffers. Capturing their first elements
            // as byrefs lets every width share the same offset without introducing Span bounds checks in
            // the hot loops. Component3 may be empty; its byref is only dereferenced for four-component operators.
            ref float c0Base = ref MemoryMarshal.GetReference(values.Component0);
            ref float c1Base = ref MemoryMarshal.GetReference(values.Component1);
            ref float c2Base = ref MemoryMarshal.GetReference(values.Component2);
            ref float c3Base = ref MemoryMarshal.GetReference(values.Component3);

            int length = values.Component0.Length;
            int i = 0;
            float scale = 1F / this.MaximumValue;

            // Descending widths keep one traversal while allowing an AVX-512 machine to process
            // an eight-pixel JPEG block with AVX2 rather than sending the entire block to scalar code.
            if (Vector512.IsHardwareAccelerated)
            {
                // Subtracting the lane count turns the loop condition into a single signed comparison.
                // A negative value naturally skips this width, and i <= end proves every unaligned
                // 64-byte reinterpretation remains entirely inside its component buffer.
                int oneVectorFromEnd = length - Vector512<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    // Precision-derived values are broadcast only when this width has work. Keeping them outside
                    // the loop avoids repeated setup without penalizing rows handled entirely by narrower widths.
                    Vector512<float> maximumValue = Vector512.Create(this.MaximumValue);
                    Vector512<float> halfValue = Vector512.Create(this.HalfValue);
                    Vector512<float> scaleVector = Vector512.Create(scale);

                    for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
                    {
                        ref Vector512<float> c0 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c0Base, i));
                        ref Vector512<float> c1 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c1Base, i));
                        ref Vector512<float> c2 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c2Base, i));

                        // ComponentCount is a static property on the closed operator type, so the JIT removes
                        // this choice. Three-component models never dereference the empty Component3 byref.
                        Vector512<float> c3 = TOperator.ComponentCount == 4
                            ? Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c3Base, i))
                            : default;

                        // c0-c2 alias the planar source vectors and are replaced in place with normalized RGB.
                        // c3 is passed by value because the fourth JPEG component must remain unchanged.
                        TOperator.ConvertToRgb(ref c0, ref c1, ref c2, c3, maximumValue, halfValue, scaleVector);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // The shared offset continues where AVX-512 stopped. At this point fewer than sixteen
                // samples remain, so this stage consumes the complete eight-sample remainder when present.
                int oneVectorFromEnd = length - Vector256<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    // YMM precision state is materialized only for an eight-sample remainder or an AVX2-only loop.
                    Vector256<float> maximumValue = Vector256.Create(this.MaximumValue);
                    Vector256<float> halfValue = Vector256.Create(this.HalfValue);
                    Vector256<float> scaleVector = Vector256.Create(scale);

                    for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
                    {
                        ref Vector256<float> c0 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c0Base, i));
                        ref Vector256<float> c1 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c1Base, i));
                        ref Vector256<float> c2 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c2Base, i));

                        // The closed operator makes this a compile-time color-model choice, not a per-vector
                        // runtime abstraction or interface dispatch.
                        Vector256<float> c3 = TOperator.ComponentCount == 4
                            ? Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c3Base, i))
                            : default;

                        TOperator.ConvertToRgb(ref c0, ref c1, ref c2, c3, maximumValue, halfValue, scaleVector);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // SSE/AdvSimd handles the final four complete samples. This also gives non-AVX machines
                // the same traversal without duplicating the control flow for another register width.
                int oneVectorFromEnd = length - Vector128<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    // XMM state is likewise created only when four samples remain for this stage.
                    Vector128<float> maximumValue = Vector128.Create(this.MaximumValue);
                    Vector128<float> halfValue = Vector128.Create(this.HalfValue);
                    Vector128<float> scaleVector = Vector128.Create(scale);

                    for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
                    {
                        ref Vector128<float> c0 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c0Base, i));
                        ref Vector128<float> c1 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c1Base, i));
                        ref Vector128<float> c2 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c2Base, i));

                        // As at the wider stages, the fourth vector is loaded only for CMYK-shaped operators.
                        Vector128<float> c3 = TOperator.ComponentCount == 4
                            ? Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c3Base, i))
                            : default;

                        TOperator.ConvertToRgb(ref c0, ref c1, ref c2, c3, maximumValue, halfValue, scaleVector);
                    }
                }
            }

            // Fewer than four samples remain after the SIMD cascade. Processing from the shared offset
            // guarantees each sample is visited exactly once for arbitrary test lengths and JPEG block rows.
            for (; i < length; i++)
            {
                float c3 = TOperator.ComponentCount == 4 ? Unsafe.Add(ref c3Base, i) : 0;

                TOperator.ConvertToRgb(
                    ref Unsafe.Add(ref c0Base, i),
                    ref Unsafe.Add(ref c1Base, i),
                    ref Unsafe.Add(ref c2Base, i),
                    c3,
                    this.MaximumValue,
                    this.HalfValue,
                    scale);
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => TOperator.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            // The encoder supplies equally sized RGB planes and destination component planes. Byrefs preserve
            // contiguous access and allow the same proven vector boundary to govern every participating lane.
            // Component3 is empty for three-component formats and is only written by four-component operators.
            ref float c0Base = ref MemoryMarshal.GetReference(values.Component0);
            ref float c1Base = ref MemoryMarshal.GetReference(values.Component1);
            ref float c2Base = ref MemoryMarshal.GetReference(values.Component2);
            ref float c3Base = ref MemoryMarshal.GetReference(values.Component3);
            ref float rBase = ref MemoryMarshal.GetReference(rLane);
            ref float gBase = ref MemoryMarshal.GetReference(gLane);
            ref float bBase = ref MemoryMarshal.GetReference(bLane);

            int length = values.Component0.Length;
            int i = 0;
            float scale = 1F / this.MaximumValue;

            // Each vector overload returns planar component vectors. Storing them here keeps the
            // operator concerned only with color arithmetic and preserves contiguous lane access.
            if (Vector512.IsHardwareAccelerated)
            {
                // The end offset proves all three 64-byte RGB reads and all component writes are in range.
                // A short row yields a negative end and falls through to the next supported width.
                int oneVectorFromEnd = length - Vector512<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    // Operators receive width-matched precision state only when this width has work, keeping
                    // invariant broadcasts outside the loop without charging narrower or scalar rows for them.
                    Vector512<float> maximumValue = Vector512.Create(this.MaximumValue);
                    Vector512<float> halfValue = Vector512.Create(this.HalfValue);
                    Vector512<float> scaleVector = Vector512.Create(scale);

                    for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
                    {
                        Vector512<float> r = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref rBase, i));
                        Vector512<float> g = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref gBase, i));
                        Vector512<float> b = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref bBase, i));

                        TOperator.ConvertFromRgb(
                            r,
                            g,
                            b,
                            maximumValue,
                            halfValue,
                            scaleVector,
                            out Vector512<float> c0,
                            out Vector512<float> c1,
                            out Vector512<float> c2,
                            out Vector512<float> c3);

                        // Outputs remain planar: each vector contains sixteen consecutive samples from one
                        // JPEG component. Static count checks prevent grayscale from touching absent planes
                        // while disappearing completely from three- and four-component specializations.
                        Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c0Base, i)) = c0;

                        if (TOperator.ComponentCount >= 2)
                        {
                            Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c1Base, i)) = c1;
                        }

                        if (TOperator.ComponentCount >= 3)
                        {
                            Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c2Base, i)) = c2;
                        }

                        if (TOperator.ComponentCount >= 4)
                        {
                            Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref c3Base, i)) = c3;
                        }
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // Continue from the AVX-512 offset so an eight-sample tail stays vectorized on AVX-512 CPUs.
                int oneVectorFromEnd = length - Vector256<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    // Materialize YMM state only for an eight-sample remainder or an AVX2-only loop.
                    Vector256<float> maximumValue = Vector256.Create(this.MaximumValue);
                    Vector256<float> halfValue = Vector256.Create(this.HalfValue);
                    Vector256<float> scaleVector = Vector256.Create(scale);

                    for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
                    {
                        Vector256<float> r = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref rBase, i));
                        Vector256<float> g = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref gBase, i));
                        Vector256<float> b = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref bBase, i));

                        TOperator.ConvertFromRgb(
                            r,
                            g,
                            b,
                            maximumValue,
                            halfValue,
                            scaleVector,
                            out Vector256<float> c0,
                            out Vector256<float> c1,
                            out Vector256<float> c2,
                            out Vector256<float> c3);

                        // Static count checks write only planes owned by this color model.
                        Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c0Base, i)) = c0;

                        if (TOperator.ComponentCount >= 2)
                        {
                            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c1Base, i)) = c1;
                        }

                        if (TOperator.ComponentCount >= 3)
                        {
                            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c2Base, i)) = c2;
                        }

                        if (TOperator.ComponentCount >= 4)
                        {
                            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref c3Base, i)) = c3;
                        }
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // The final SIMD stage consumes four complete RGB samples on SSE or AdvSimd hardware.
                int oneVectorFromEnd = length - Vector128<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    // Materialize XMM state only when the final SIMD stage can consume four samples.
                    Vector128<float> maximumValue = Vector128.Create(this.MaximumValue);
                    Vector128<float> halfValue = Vector128.Create(this.HalfValue);
                    Vector128<float> scaleVector = Vector128.Create(scale);

                    for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
                    {
                        Vector128<float> r = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref rBase, i));
                        Vector128<float> g = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref gBase, i));
                        Vector128<float> b = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref bBase, i));

                        TOperator.ConvertFromRgb(
                            r,
                            g,
                            b,
                            maximumValue,
                            halfValue,
                            scaleVector,
                            out Vector128<float> c0,
                            out Vector128<float> c1,
                            out Vector128<float> c2,
                            out Vector128<float> c3);

                        // Four results are stored only for the planes represented by the closed operator.
                        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c0Base, i)) = c0;

                        if (TOperator.ComponentCount >= 2)
                        {
                            Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c1Base, i)) = c1;
                        }

                        if (TOperator.ComponentCount >= 3)
                        {
                            Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c2Base, i)) = c2;
                        }

                        if (TOperator.ComponentCount >= 4)
                        {
                            Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref c3Base, i)) = c3;
                        }
                    }
                }
            }

            // Scalar conversion is reserved for the zero-to-three samples that cannot fill Vector128.
            for (; i < length; i++)
            {
                TOperator.ConvertFromRgb(
                    Unsafe.Add(ref rBase, i),
                    Unsafe.Add(ref gBase, i),
                    Unsafe.Add(ref bBase, i),
                    this.MaximumValue,
                    this.HalfValue,
                    scale,
                    out float c0,
                    out float c1,
                    out float c2,
                    out float c3);

                Unsafe.Add(ref c0Base, i) = c0;

                if (TOperator.ComponentCount >= 2)
                {
                    Unsafe.Add(ref c1Base, i) = c1;
                }

                if (TOperator.ComponentCount >= 3)
                {
                    Unsafe.Add(ref c2Base, i) = c2;
                }

                if (TOperator.ComponentCount >= 4)
                {
                    Unsafe.Add(ref c3Base, i) = c3;
                }
            }
        }
    }
}

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
        public static abstract void ConvertToRgb(ref float c0, ref float c1, ref float c2, float c3, float maximumValue, float halfValue, float scale);

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
        /// Gets the normalized chroma offset used to encode profile components.
        /// </summary>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The integer JPEG chroma midpoint.</param>
        /// <returns>The offset applied before scaling profile components to JPEG codes.</returns>
        public static virtual float GetProfileChromaOffset(float maximumValue, float halfValue) => 0F;

        /// <summary>
        /// Converts JPEG component values to normalized profile components.
        /// </summary>
        /// <param name="c0">The first component, replaced in place.</param>
        /// <param name="c1">The second component, replaced in place when used.</param>
        /// <param name="c2">The third component, replaced in place when used.</param>
        /// <param name="c3">The fourth component, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The integer JPEG chroma midpoint.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertToProfile(
            ref float c0,
            ref float c1,
            ref float c2,
            ref float c3,
            float maximumValue,
            float halfValue,
            float scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts JPEG component values to normalized profile components.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced in place.</param>
        /// <param name="c1">The second component lanes, replaced in place when used.</param>
        /// <param name="c2">The third component lanes, replaced in place when used.</param>
        /// <param name="c3">The fourth component lanes, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The integer JPEG chroma midpoint.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertToProfile(
            ref Vector128<float> c0,
            ref Vector128<float> c1,
            ref Vector128<float> c2,
            ref Vector128<float> c3,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts JPEG component values to normalized profile components.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced in place.</param>
        /// <param name="c1">The second component lanes, replaced in place when used.</param>
        /// <param name="c2">The third component lanes, replaced in place when used.</param>
        /// <param name="c3">The fourth component lanes, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The integer JPEG chroma midpoint.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertToProfile(
            ref Vector256<float> c0,
            ref Vector256<float> c1,
            ref Vector256<float> c2,
            ref Vector256<float> c3,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts JPEG component values to normalized profile components.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced in place.</param>
        /// <param name="c1">The second component lanes, replaced in place when used.</param>
        /// <param name="c2">The third component lanes, replaced in place when used.</param>
        /// <param name="c3">The fourth component lanes, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The integer JPEG chroma midpoint.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertToProfile(
            ref Vector512<float> c0,
            ref Vector512<float> c1,
            ref Vector512<float> c2,
            ref Vector512<float> c3,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts normalized profile components to JPEG component values.
        /// </summary>
        /// <param name="c0">The first component, replaced in place.</param>
        /// <param name="c1">The second component, replaced in place when used.</param>
        /// <param name="c2">The third component, replaced in place when used.</param>
        /// <param name="c3">The fourth component, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The normalized chroma offset selected by the operator.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertFromProfile(
            ref float c0,
            ref float c1,
            ref float c2,
            ref float c3,
            float maximumValue,
            float halfValue,
            float scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts normalized profile components to JPEG component values.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced in place.</param>
        /// <param name="c1">The second component lanes, replaced in place when used.</param>
        /// <param name="c2">The third component lanes, replaced in place when used.</param>
        /// <param name="c3">The fourth component lanes, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The normalized chroma offset selected by the operator.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertFromProfile(
            ref Vector128<float> c0,
            ref Vector128<float> c1,
            ref Vector128<float> c2,
            ref Vector128<float> c3,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts normalized profile components to JPEG component values.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced in place.</param>
        /// <param name="c1">The second component lanes, replaced in place when used.</param>
        /// <param name="c2">The third component lanes, replaced in place when used.</param>
        /// <param name="c3">The fourth component lanes, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The normalized chroma offset selected by the operator.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertFromProfile(
            ref Vector256<float> c0,
            ref Vector256<float> c1,
            ref Vector256<float> c2,
            ref Vector256<float> c3,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            IccColorSpaceType profileSpace);

        /// <summary>
        /// Converts normalized profile components to JPEG component values.
        /// </summary>
        /// <param name="c0">The first component lanes, replaced in place.</param>
        /// <param name="c1">The second component lanes, replaced in place when used.</param>
        /// <param name="c2">The third component lanes, replaced in place when used.</param>
        /// <param name="c3">The fourth component lanes, replaced in place when used.</param>
        /// <param name="maximumValue">The maximum JPEG sample code.</param>
        /// <param name="halfValue">The normalized chroma offset selected by the operator.</param>
        /// <param name="scale">The reciprocal maximum JPEG sample code.</param>
        /// <param name="profileSpace">The profile's device color space, used to select the required JPEG model transform.</param>
        public static abstract void ConvertFromProfile(
            ref Vector512<float> c0,
            ref Vector512<float> c1,
            ref Vector512<float> c2,
            ref Vector512<float> c3,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            IccColorSpaceType profileSpace);
    }

    /// <summary>
    /// Converts a JPEG color model using a single operator-driven traversal for all SIMD widths.
    /// </summary>
    /// <typeparam name="TOperator">The color-model-specific arithmetic.</typeparam>
    internal sealed partial class JpegColorConverter<TOperator> : JpegColorConverterBase
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
                : Vector256.IsHardwareAccelerated ? Vector256<float>.Count : Vector128.IsHardwareAccelerated ? Vector128<float>.Count : 1;

        /// <inheritdoc/>
        public override void ConvertToRgb(in ComponentValues values, Span<byte> red, Span<byte> green, Span<byte> blue)
        {
            // JPEG component processors own equally sized planar buffers, and the destination planes have the
            // visible row length. Byrefs let every width share one offset without Span bounds checks in the hot loops.
            // Component3 can be empty. Only four-component operators read it.
            RowPlanes planes = new(in values, red, green, blue);
            int length = red.Length;
            int i = 0;
            float scale = 1F / this.MaximumValue;

            // Each step converts four float vectors of each component in registers, then packs them to one byte
            // vector for each RGB plane. The normalized RGB values never go back to memory.
            // The component processors clamp every sample to [0, MaximumValue], and each operator is a bounded
            // function of its samples. Thus, all normalized values are finite and small. The finite byte conversion
            // then gives the same bytes as the span conversion, which clamps, because its saturating narrowing does the clamp.
            // The widths go from widest to narrowest in one traversal. A row remainder goes to the next narrower width.
            if (Vector512.IsHardwareAccelerated)
            {
                // A negative end skips this width. The condition i <= end proves that all 64 samples are in the row.
                int end = length - Vector512<byte>.Count;
                if (i <= end)
                {
                    Vector512<float> maximumValue = Vector512.Create(this.MaximumValue);
                    Vector512<float> halfValue = Vector512.Create(this.HalfValue);
                    Vector512<float> scaleVector = Vector512.Create(scale);
                    for (; i <= end; i += Vector512<byte>.Count)
                    {
                        ConvertToRgbStep(in planes, (uint)i, maximumValue, halfValue, scaleVector);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // If Vector512 is accelerated, fewer than 64 samples remain. This width converts one step of 32 samples
                // if 32 samples remain. Otherwise, this width starts at the start of the row.
                int end = length - Vector256<byte>.Count;
                if (i <= end)
                {
                    Vector256<float> maximumValue = Vector256.Create(this.MaximumValue);
                    Vector256<float> halfValue = Vector256.Create(this.HalfValue);
                    Vector256<float> scaleVector = Vector256.Create(scale);
                    for (; i <= end; i += Vector256<byte>.Count)
                    {
                        ConvertToRgbStep(in planes, (uint)i, maximumValue, halfValue, scaleVector);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // If Vector256 is accelerated, fewer than 32 samples remain. This width converts one step of 16 samples
                // if 16 samples remain. Otherwise, this width starts at the start of the row.
                int end = length - Vector128<byte>.Count;
                if (i <= end)
                {
                    Vector128<float> maximumValue = Vector128.Create(this.MaximumValue);
                    Vector128<float> halfValue = Vector128.Create(this.HalfValue);
                    Vector128<float> scaleVector = Vector128.Create(scale);
                    for (; i <= end; i += Vector128<byte>.Count)
                    {
                        ConvertToRgbStep(in planes, (uint)i, maximumValue, halfValue, scaleVector);
                    }
                }
            }

            // Fewer than 16 samples remain after the SIMD cascade, or the full row remains without SIMD.
            // The scalar operator writes normalized RGB to small stack planes. SimdUtils then converts each plane
            // to bytes with its own rule, so the converter does not repeat the byte rounding rule.
            const int chunkLength = 16;
            Span<float> rChunk = stackalloc float[chunkLength];
            Span<float> gChunk = stackalloc float[chunkLength];
            Span<float> bChunk = stackalloc float[chunkLength];
            while (i < length)
            {
                int count = Math.Min(chunkLength, length - i);
                for (int j = 0; j < count; j++)
                {
                    nuint o = (uint)(i + j);
                    float c0 = Unsafe.Add(ref planes.Component0, o);
                    float c1 = Unsafe.Add(ref planes.Component1, o);
                    float c2 = Unsafe.Add(ref planes.Component2, o);
                    float c3 = TOperator.ComponentCount == 4 ? Unsafe.Add(ref planes.Component3, o) : 0;

                    TOperator.ConvertToRgb(ref c0, ref c1, ref c2, c3, this.MaximumValue, this.HalfValue, scale);
                    rChunk[j] = c0;
                    gChunk[j] = c1;
                    bChunk[j] = c2;
                }

                SimdUtils.NormalizedFloatToByteSaturate(rChunk[..count], red.Slice(i, count));
                SimdUtils.NormalizedFloatToByteSaturate(gChunk[..count], green.Slice(i, count));
                SimdUtils.NormalizedFloatToByteSaturate(bChunk[..count], blue.Slice(i, count));
                i += count;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(ReadOnlySpan<float> rLane, ReadOnlySpan<float> gLane, ReadOnlySpan<float> bLane, in ComponentValues destination)
        {
            // The encoder supplies equally sized RGB planes and destination component planes. Byrefs preserve
            // contiguous access and allow the same proven vector boundary to govern every participating lane.
            // Component3 is empty for three-component formats and is only written by four-component operators.
            ref float c0Base = ref MemoryMarshal.GetReference(destination.Component0);
            ref float c1Base = ref MemoryMarshal.GetReference(destination.Component1);
            ref float c2Base = ref MemoryMarshal.GetReference(destination.Component2);
            ref float c3Base = ref MemoryMarshal.GetReference(destination.Component3);
            ref float rBase = ref MemoryMarshal.GetReference(rLane);
            ref float gBase = ref MemoryMarshal.GetReference(gLane);
            ref float bBase = ref MemoryMarshal.GetReference(bLane);

            int length = destination.Component0.Length;
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

        /// <summary>
        /// Converts 64 samples of each component to one <see cref="Vector512{T}"/> of bytes for each RGB plane.
        /// </summary>
        /// <param name="planes">The source and destination planes.</param>
        /// <param name="offset">The first sample of the step.</param>
        /// <param name="max">The maximum component value for the configured precision.</param>
        /// <param name="half">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="max"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertToRgbStep(
            in RowPlanes planes,
            nuint offset,
            Vector512<float> max,
            Vector512<float> half,
            Vector512<float> scale)
        {
            // Vector k holds the samples offset + 16k to offset + 16k + 15 of each component.
            LoadRgb(in planes, offset, max, half, scale, out Vector512<float> r0, out Vector512<float> g0, out Vector512<float> b0);
            LoadRgb(in planes, offset + 16, max, half, scale, out Vector512<float> r1, out Vector512<float> g1, out Vector512<float> b1);
            LoadRgb(in planes, offset + 32, max, half, scale, out Vector512<float> r2, out Vector512<float> g2, out Vector512<float> b2);
            LoadRgb(in planes, offset + 48, max, half, scale, out Vector512<float> r3, out Vector512<float> g3, out Vector512<float> b3);

            // One byte vector holds the bytes of the four float vectors of one plane, in source order.
            Vector512<float> byteScale = Vector512.Create((float)byte.MaxValue);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(r0, r1, r2, r3, byteScale).StoreUnsafe(ref planes.Red, offset);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(g0, g1, g2, g3, byteScale).StoreUnsafe(ref planes.Green, offset);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(b0, b1, b2, b3, byteScale).StoreUnsafe(ref planes.Blue, offset);
        }

        /// <summary>
        /// Converts 32 samples of each component to one <see cref="Vector256{T}"/> of bytes for each RGB plane.
        /// </summary>
        /// <param name="planes">The source and destination planes.</param>
        /// <param name="offset">The first sample of the step.</param>
        /// <param name="max">The maximum component value for the configured precision.</param>
        /// <param name="half">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="max"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertToRgbStep(
            in RowPlanes planes,
            nuint offset,
            Vector256<float> max,
            Vector256<float> half,
            Vector256<float> scale)
        {
            // Vector k holds the samples offset + 8k to offset + 8k + 7 of each component.
            LoadRgb(in planes, offset, max, half, scale, out Vector256<float> r0, out Vector256<float> g0, out Vector256<float> b0);
            LoadRgb(in planes, offset + 8, max, half, scale, out Vector256<float> r1, out Vector256<float> g1, out Vector256<float> b1);
            LoadRgb(in planes, offset + 16, max, half, scale, out Vector256<float> r2, out Vector256<float> g2, out Vector256<float> b2);
            LoadRgb(in planes, offset + 24, max, half, scale, out Vector256<float> r3, out Vector256<float> g3, out Vector256<float> b3);

            // One byte vector holds the bytes of the four float vectors of one plane, in source order.
            Vector256<float> byteScale = Vector256.Create((float)byte.MaxValue);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(r0, r1, r2, r3, byteScale).StoreUnsafe(ref planes.Red, offset);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(g0, g1, g2, g3, byteScale).StoreUnsafe(ref planes.Green, offset);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(b0, b1, b2, b3, byteScale).StoreUnsafe(ref planes.Blue, offset);
        }

        /// <summary>
        /// Converts 16 samples of each component to one <see cref="Vector128{T}"/> of bytes for each RGB plane.
        /// </summary>
        /// <param name="planes">The source and destination planes.</param>
        /// <param name="offset">The first sample of the step.</param>
        /// <param name="max">The maximum component value for the configured precision.</param>
        /// <param name="half">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="max"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertToRgbStep(
            in RowPlanes planes,
            nuint offset,
            Vector128<float> max,
            Vector128<float> half,
            Vector128<float> scale)
        {
            // Vector k holds the samples offset + 4k to offset + 4k + 3 of each component.
            LoadRgb(in planes, offset, max, half, scale, out Vector128<float> r0, out Vector128<float> g0, out Vector128<float> b0);
            LoadRgb(in planes, offset + 4, max, half, scale, out Vector128<float> r1, out Vector128<float> g1, out Vector128<float> b1);
            LoadRgb(in planes, offset + 8, max, half, scale, out Vector128<float> r2, out Vector128<float> g2, out Vector128<float> b2);
            LoadRgb(in planes, offset + 12, max, half, scale, out Vector128<float> r3, out Vector128<float> g3, out Vector128<float> b3);

            // One byte vector holds the bytes of the four float vectors of one plane, in source order.
            Vector128<float> byteScale = Vector128.Create((float)byte.MaxValue);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(r0, r1, r2, r3, byteScale).StoreUnsafe(ref planes.Red, offset);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(g0, g1, g2, g3, byteScale).StoreUnsafe(ref planes.Green, offset);
            SimdUtils.HwIntrinsics.FloatToByteSaturate(b0, b1, b2, b3, byteScale).StoreUnsafe(ref planes.Blue, offset);
        }

        /// <summary>
        /// Loads one <see cref="Vector512{T}"/> of each component and converts it to normalized RGB in registers.
        /// </summary>
        /// <param name="planes">The source planes.</param>
        /// <param name="offset">The first sample of the vector.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/>.</param>
        /// <param name="r">The normalized red values.</param>
        /// <param name="g">The normalized green values.</param>
        /// <param name="b">The normalized blue values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadRgb(
            in RowPlanes planes,
            nuint offset,
            Vector512<float> maximumValue,
            Vector512<float> halfValue,
            Vector512<float> scale,
            out Vector512<float> r,
            out Vector512<float> g,
            out Vector512<float> b)
        {
            // The operator replaces the component locals with RGB. The locals do not alias,
            // so a grayscale row, where the three planes are one buffer, is also safe.
            // ComponentCount is a constant of the closed operator type. The JIT removes the c3 load for three-component models.
            r = Vector512.LoadUnsafe(ref planes.Component0, offset);
            g = Vector512.LoadUnsafe(ref planes.Component1, offset);
            b = Vector512.LoadUnsafe(ref planes.Component2, offset);
            Vector512<float> c3 = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref planes.Component3, offset) : default;
            TOperator.ConvertToRgb(ref r, ref g, ref b, c3, maximumValue, halfValue, scale);
        }

        /// <summary>
        /// Loads one <see cref="Vector256{T}"/> of each component and converts it to normalized RGB in registers.
        /// </summary>
        /// <param name="planes">The source planes.</param>
        /// <param name="offset">The first sample of the vector.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/>.</param>
        /// <param name="r">The normalized red values.</param>
        /// <param name="g">The normalized green values.</param>
        /// <param name="b">The normalized blue values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadRgb(
            in RowPlanes planes,
            nuint offset,
            Vector256<float> maximumValue,
            Vector256<float> halfValue,
            Vector256<float> scale,
            out Vector256<float> r,
            out Vector256<float> g,
            out Vector256<float> b)
        {
            // See the Vector512 overload.
            r = Vector256.LoadUnsafe(ref planes.Component0, offset);
            g = Vector256.LoadUnsafe(ref planes.Component1, offset);
            b = Vector256.LoadUnsafe(ref planes.Component2, offset);
            Vector256<float> c3 = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref planes.Component3, offset) : default;
            TOperator.ConvertToRgb(ref r, ref g, ref b, c3, maximumValue, halfValue, scale);
        }

        /// <summary>
        /// Loads one <see cref="Vector128{T}"/> of each component and converts it to normalized RGB in registers.
        /// </summary>
        /// <param name="planes">The source planes.</param>
        /// <param name="offset">The first sample of the vector.</param>
        /// <param name="maximumValue">The maximum component value for the configured precision.</param>
        /// <param name="halfValue">The midpoint component value for the configured precision.</param>
        /// <param name="scale">The reciprocal of <paramref name="maximumValue"/>.</param>
        /// <param name="r">The normalized red values.</param>
        /// <param name="g">The normalized green values.</param>
        /// <param name="b">The normalized blue values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadRgb(
            in RowPlanes planes,
            nuint offset,
            Vector128<float> maximumValue,
            Vector128<float> halfValue,
            Vector128<float> scale,
            out Vector128<float> r,
            out Vector128<float> g,
            out Vector128<float> b)
        {
            // See the Vector512 overload.
            r = Vector128.LoadUnsafe(ref planes.Component0, offset);
            g = Vector128.LoadUnsafe(ref planes.Component1, offset);
            b = Vector128.LoadUnsafe(ref planes.Component2, offset);
            Vector128<float> c3 = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref planes.Component3, offset) : default;
            TOperator.ConvertToRgb(ref r, ref g, ref b, c3, maximumValue, halfValue, scale);
        }

        /// <summary>
        /// The first samples of the component planes of a row, and of its destination RGB planes.
        /// </summary>
        private readonly ref struct RowPlanes
        {
            /// <summary>
            /// The first sample of the first component.
            /// </summary>
            public readonly ref float Component0;

            /// <summary>
            /// The first sample of the second component.
            /// </summary>
            public readonly ref float Component1;

            /// <summary>
            /// The first sample of the third component.
            /// </summary>
            public readonly ref float Component2;

            /// <summary>
            /// The first sample of the fourth component. It is valid only for four-component operators.
            /// </summary>
            public readonly ref float Component3;

            /// <summary>
            /// The first red byte.
            /// </summary>
            public readonly ref byte Red;

            /// <summary>
            /// The first green byte.
            /// </summary>
            public readonly ref byte Green;

            /// <summary>
            /// The first blue byte.
            /// </summary>
            public readonly ref byte Blue;

            /// <summary>
            /// Initializes a new instance of the <see cref="RowPlanes"/> struct.
            /// </summary>
            /// <param name="values">The component planes.</param>
            /// <param name="red">The destination red plane.</param>
            /// <param name="green">The destination green plane.</param>
            /// <param name="blue">The destination blue plane.</param>
            public RowPlanes(in ComponentValues values, Span<byte> red, Span<byte> green, Span<byte> blue)
            {
                this.Component0 = ref MemoryMarshal.GetReference(values.Component0);
                this.Component1 = ref MemoryMarshal.GetReference(values.Component1);
                this.Component2 = ref MemoryMarshal.GetReference(values.Component2);
                this.Component3 = ref MemoryMarshal.GetReference(values.Component3);
                this.Red = ref MemoryMarshal.GetReference(red);
                this.Green = ref MemoryMarshal.GetReference(green);
                this.Blue = ref MemoryMarshal.GetReference(blue);
            }
        }
    }
}

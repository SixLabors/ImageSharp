// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Defines component arithmetic independently of row traversal and pixel storage.
    /// </summary>
    /// <remarks>Each scalar and vector overload applies the same operation to independent pixels.</remarks>
    public interface IJxlColorConverterOperator
    {
        /// <summary>
        /// Gets the number of source device components.
        /// </summary>
        public static abstract int ComponentCount { get; }

        /// <summary>
        /// Gets the packed float stride used by the shared color conversion.
        /// </summary>
        public static abstract int PackedComponentCount { get; }

        /// <summary>
        /// Gets the additional float stride required by the shared color-model conversion.
        /// </summary>
        public static abstract int ScratchComponentCount { get; }

        /// <summary>
        /// Orders and converts the normalized device components of one pixel.
        /// </summary>
        /// <param name="c0">The first component.</param>
        /// <param name="c1">The second component.</param>
        /// <param name="c2">The third component.</param>
        /// <param name="c3">The fourth device component, or zero when unused.</param>
        public static abstract void ConvertToProfile(ref float c0, ref float c1, ref float c2, ref float c3);

        /// <summary>
        /// Orders and converts the normalized device components of four pixels.
        /// </summary>
        /// <param name="c0">The first component.</param>
        /// <param name="c1">The second component.</param>
        /// <param name="c2">The third component.</param>
        /// <param name="c3">The fourth device component, or zero when unused.</param>
        public static abstract void ConvertToProfile(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3);

        /// <summary>
        /// Orders and converts the normalized device components of eight pixels.
        /// </summary>
        /// <param name="c0">The first component.</param>
        /// <param name="c1">The second component.</param>
        /// <param name="c2">The third component.</param>
        /// <param name="c3">The fourth device component, or zero when unused.</param>
        public static abstract void ConvertToProfile(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3);

        /// <summary>
        /// Orders and converts the normalized device components of sixteen pixels.
        /// </summary>
        /// <param name="c0">The first component.</param>
        /// <param name="c1">The second component.</param>
        /// <param name="c2">The third component.</param>
        /// <param name="c3">The fourth device component, or zero when unused.</param>
        public static abstract void ConvertToProfile(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3);

        /// <summary>
        /// Converts packed profile components through the existing shared color APIs.
        /// </summary>
        /// <param name="packed">The normalized source components in profile order.</param>
        /// <param name="destination">The working sRGB vectors.</param>
        /// <param name="scratch">The reusable three-component row, separate from four-component device storage.</param>
        /// <param name="converter">The prepared source profile and working-space conversion.</param>
        public static abstract void ConvertToRgb(Span<float> packed, Span<Vector4> destination, Span<float> scratch, ColorProfileConverter converter);
    }

    /// <summary>
    /// Traverses rows for one selected component operation.
    /// </summary>
    /// <typeparam name="TOperator">The component operation selected before row traversal.</typeparam>
    private sealed class JxlColorConverter<TOperator> : JxlColorConverterBase
        where TOperator : struct, IJxlColorConverterOperator
    {
        /// <summary>
        /// The immutable conversion selected for this image.
        /// </summary>
        private readonly ColorProfileConverter converter;

        /// <summary>
        /// Initializes a new instance of the <see cref="JxlColorConverter{TOperator}"/> class.
        /// </summary>
        /// <param name="converter">The prepared source-to-working-space conversion.</param>
        public JxlColorConverter(ColorProfileConverter converter) => this.converter = converter;

        /// <inheritdoc/>
        public override long GetBufferLength(int width) => (long)width * TOperator.ScratchComponentCount;

        /// <inheritdoc/>
        public override void ConvertToRgb<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<Vector4> destination,
            Span<float> scratch,
            in TSampleOperator sampleOperator)
        {
            int count = source.Component0.Length;
            int channels = TOperator.PackedComponentCount;

            // Three-component models use the separate packed row. Keep it distinct from the four-component
            // destination while the shared converter reads the source. ICC consumes the destination vectors directly.
            Span<float> packed = channels == 4 ? MemoryMarshal.Cast<Vector4, float>(destination) : scratch[..(count * channels)];
            ref TSample c0 = ref MemoryMarshal.GetReference(source.Component0);
            ref TSample c1 = ref MemoryMarshal.GetReference(source.Component1);
            ref TSample c2 = ref MemoryMarshal.GetReference(source.Component2);
            ref float black = ref MemoryMarshal.GetReference(source.Black);
            ref float target = ref MemoryMarshal.GetReference(packed);
            int i = 0;

            // Every source lane holds one pixel. Sample conversion and component arithmetic remain in
            // registers until the shared interleave stores profile order. The closed operator fixes both
            // component counts, so the JIT removes unused black loads and unused packing alternatives.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = source.Component0[i..].Vector512Count();
                for (nuint block = 0; block < vectorCount; block++, i += Vector512<TSample>.Count)
                {
                    Vector512<float> x = sampleOperator.Invoke(Vector512.LoadUnsafe(ref c0, (nuint)i));
                    Vector512<float> y = sampleOperator.Invoke(Vector512.LoadUnsafe(ref c1, (nuint)i));
                    Vector512<float> z = sampleOperator.Invoke(Vector512.LoadUnsafe(ref c2, (nuint)i));
                    Vector512<float> k = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref black, (nuint)i) : default;
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k);
                    ref float output = ref Unsafe.Add(ref target, (nuint)i * (uint)channels);

                    if (channels == 4)
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref output);
                    }
                    else
                    {
                        SimdUtils.Interleave3(x, y, z, ref output);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = source.Component0[i..].Vector256Count();
                for (nuint block = 0; block < vectorCount; block++, i += Vector256<TSample>.Count)
                {
                    Vector256<float> x = sampleOperator.Invoke(Vector256.LoadUnsafe(ref c0, (nuint)i));
                    Vector256<float> y = sampleOperator.Invoke(Vector256.LoadUnsafe(ref c1, (nuint)i));
                    Vector256<float> z = sampleOperator.Invoke(Vector256.LoadUnsafe(ref c2, (nuint)i));
                    Vector256<float> k = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref black, (nuint)i) : default;
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k);
                    ref float output = ref Unsafe.Add(ref target, (nuint)i * (uint)channels);

                    if (channels == 4)
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref output);
                    }
                    else
                    {
                        SimdUtils.Interleave3(x, y, z, ref output);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = source.Component0[i..].Vector128Count();
                for (nuint block = 0; block < vectorCount; block++, i += Vector128<TSample>.Count)
                {
                    Vector128<float> x = sampleOperator.Invoke(Vector128.LoadUnsafe(ref c0, (nuint)i));
                    Vector128<float> y = sampleOperator.Invoke(Vector128.LoadUnsafe(ref c1, (nuint)i));
                    Vector128<float> z = sampleOperator.Invoke(Vector128.LoadUnsafe(ref c2, (nuint)i));
                    Vector128<float> k = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref black, (nuint)i) : default;
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k);
                    ref float output = ref Unsafe.Add(ref target, (nuint)i * (uint)channels);

                    if (channels == 4)
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref output);
                    }
                    else
                    {
                        SimdUtils.Interleave3(x, y, z, ref output);
                    }
                }
            }

            // The remainder uses the same prepared sample operator and component equations. No rounded
            // pixel representation or storage clamp is introduced between reconstruction and color conversion.
            for (; i < count; i++)
            {
                float x = sampleOperator.Invoke(Unsafe.Add(ref c0, i));
                float y = sampleOperator.Invoke(Unsafe.Add(ref c1, i));
                float z = sampleOperator.Invoke(Unsafe.Add(ref c2, i));
                float k = TOperator.ComponentCount == 4 ? Unsafe.Add(ref black, i) : 0F;
                TOperator.ConvertToProfile(ref x, ref y, ref z, ref k);
                int offset = i * channels;
                Unsafe.Add(ref target, offset) = x;
                Unsafe.Add(ref target, offset + 1) = y;
                Unsafe.Add(ref target, offset + 2) = z;

                if (channels == 4)
                {
                    Unsafe.Add(ref target, offset + 3) = k;
                }
            }

            TOperator.ConvertToRgb(packed, destination, scratch, this.converter);

            // ICC's fourth component can be black and has no alpha meaning. Restore alpha only after
            // the color operation. PixelOperations applies association and destination bounds afterward.
            if (source.Alpha.IsEmpty)
            {
                Numerics.WithW(destination, 1F);
            }
            else
            {
                Numerics.WithW(destination, source.Alpha);
            }
        }
    }
}

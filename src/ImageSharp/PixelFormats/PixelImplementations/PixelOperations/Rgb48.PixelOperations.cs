// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Rgb48
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal partial class PixelOperations : PixelOperations<Rgb48>
    {
        /// <inheritdoc />
        public override void ToVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        public override void ToScaledVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ReadOnlySpan<ushort> packed = MemoryMarshal.Cast<Rgb48, ushort>(source);
            ref float output = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int i = 0;

            // Every ushort fits in a positive int. Use signed conversion so older SIMD targets need no uint conversion emulation.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint groups = (source.Length - i).Vector512Count<float>();
                Vector512<float> maximum = Vector512.Create((float)ushort.MaxValue);

                // Each register holds one component from 16 pixels. Divide before adding opaque alpha, then pack complete RGBA vectors.
                for (nuint group = 0; group < groups; group++, i += Vector512<float>.Count)
                {
                    SimdUtils.Deinterleave3(packed, i, out Vector256<ushort> r, out Vector256<ushort> g, out Vector256<ushort> b);
                    Vector512<float> red = Vector512.ConvertToSingle(Vector512.WidenLower(r.ToVector512()).AsInt32()) / maximum;
                    Vector512<float> green = Vector512.ConvertToSingle(Vector512.WidenLower(g.ToVector512()).AsInt32()) / maximum;
                    Vector512<float> blue = Vector512.ConvertToSingle(Vector512.WidenLower(b.ToVector512()).AsInt32()) / maximum;
                    SimdUtils.Interleave4(red, green, blue, Vector512.Create(1F), ref Unsafe.Add(ref output, (uint)i * 4));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint groups = (source.Length - i).Vector256Count<float>();
                Vector256<float> maximum = Vector256.Create((float)ushort.MaxValue);

                // Each register holds one component from 8 pixels. Divide before adding opaque alpha, then pack complete RGBA vectors.
                for (nuint group = 0; group < groups; group++, i += Vector256<float>.Count)
                {
                    SimdUtils.Deinterleave3(packed, i, out Vector128<ushort> r, out Vector128<ushort> g, out Vector128<ushort> b);
                    Vector256<float> red = Vector256.ConvertToSingle(Vector256.WidenLower(r.ToVector256()).AsInt32()) / maximum;
                    Vector256<float> green = Vector256.ConvertToSingle(Vector256.WidenLower(g.ToVector256()).AsInt32()) / maximum;
                    Vector256<float> blue = Vector256.ConvertToSingle(Vector256.WidenLower(b.ToVector256()).AsInt32()) / maximum;
                    SimdUtils.Interleave4(red, green, blue, Vector256.Create(1F), ref Unsafe.Add(ref output, (uint)i * 4));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint groups = (source.Length - i).Vector128Count<float>();
                Vector128<float> maximum = Vector128.Create((float)ushort.MaxValue);

                // Each register holds one component from 4 pixels. Divide before adding opaque alpha, then pack complete RGBA vectors.
                for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
                {
                    SimdUtils.Deinterleave3(packed, i, out Vector64<ushort> r, out Vector64<ushort> g, out Vector64<ushort> b);
                    Vector128<float> red = Vector128.ConvertToSingle(Vector128.WidenLower(r.ToVector128()).AsInt32()) / maximum;
                    Vector128<float> green = Vector128.ConvertToSingle(Vector128.WidenLower(g.ToVector128()).AsInt32()) / maximum;
                    Vector128<float> blue = Vector128.ConvertToSingle(Vector128.WidenLower(b.ToVector128()).AsInt32()) / maximum;
                    SimdUtils.Interleave4(red, green, blue, Vector128.Create(1F), ref Unsafe.Add(ref output, (uint)i * 4));
                }
            }

            // The final pixels use the same division and opaque alpha as the vector paths.
            for (; i < source.Length; i++)
            {
                destination[i] = source[i].ToVector4();
            }
        }

        // Alpha is implicitly one, so both outward representations already contain associated color components.

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<Vector4> destination)
            => this.ToUnassociatedScaledVector4(configuration, source, destination);
    }
}

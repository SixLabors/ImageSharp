// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares bulk conversion for consecutive unsigned 16-bit RGB and RGBA storage.
/// </summary>
/// <remarks>
/// Components divide by 65535 in the same order as the scalar pixel API. RGB storage supplies opaque alpha.
/// Packing uses the shared finite bounds and midpoint-to-even rounding. RGB storage discards the input alpha after any required unassociation.
/// </remarks>
/// <typeparam name="TPixel">The RGB48- or RGBA64-compatible pixel format.</typeparam>
internal abstract class Rgba64CompatiblePixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <inheritdoc />
    public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // RGBA words already have vector component order, so widen directly into final storage.
            SimdUtils.UInt16ToNormalizedFloat(MemoryMarshal.Cast<TPixel, ushort>(source), MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }
        else
        {
            UnpackRgb48(configuration, source, destination);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToUnassociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        // Formats without alpha supply opaque output, so the associated vectors are the straight vectors.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.Unassociated)
        {
            this.ToUnassociatedVector4(configuration, source, destination);
            return;
        }

        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // RGBA words already have vector component order. The conversion normalizes and associates each pixel in one pass.
        SimdUtils.UInt16ToNormalizedFloat<PixelFormats.PixelOperations.AssociateOperator>(
            MemoryMarshal.Cast<TPixel, ushort>(source),
            MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
    }

    /// <inheritdoc />
    public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToAssociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromUnassociatedScaledVector4Destructive(configuration, source, destination);

    /// <inheritdoc />
    public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Pack(source, destination, false);
    }

    /// <inheritdoc />
    public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromAssociatedScaledVector4Destructive(configuration, source, destination);

    /// <inheritdoc />
    public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Pack(source, destination, true);
    }

    /// <inheritdoc />
    public override void FromL16(Configuration configuration, ReadOnlySpan<L16> source, Span<TPixel> destination)
        => FromRgba64Source<PixelConverter.L16Rgba64Operator, L16>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromLa32(Configuration configuration, ReadOnlySpan<La32> source, Span<TPixel> destination)
        => FromRgba64Source<PixelConverter.La32Rgba64Operator, La32>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgb48(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<TPixel> destination)
        => FromRgba64Source<PixelConverter.Rgb48Rgba64Operator, Rgb48>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgba64(Configuration configuration, ReadOnlySpan<Rgba64> source, Span<TPixel> destination)
        => FromRgba64Source<PixelConverter.Rgba64Rgba64Operator, Rgba64>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgba32(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<TPixel> destination)
    {
        // Rgba32 has the channel order of Rgba64, so an RGBA destination scales the source bytes directly.
        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelConverter.ScaleUnorm8To16(MemoryMarshal.AsBytes(source), MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]));
            return;
        }

        FromByteSource(configuration, source, destination);
    }

    /// <inheritdoc />
    public override void FromRgb24(Configuration configuration, ReadOnlySpan<Rgb24> source, Span<TPixel> destination)
    {
        // Rgb24 has the channel order of Rgb48, so an RGB destination scales the source bytes directly.
        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 3)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelConverter.ScaleUnorm8To16(MemoryMarshal.AsBytes(source), MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]));
            return;
        }

        FromByteSource(configuration, source, destination);
    }

    /// <inheritdoc />
    public override void FromBgra32(Configuration configuration, ReadOnlySpan<Bgra32> source, Span<TPixel> destination)
        => FromByteSource(configuration, source, destination);

    /// <inheritdoc />
    public override void FromArgb32(Configuration configuration, ReadOnlySpan<Argb32> source, Span<TPixel> destination)
        => FromByteSource(configuration, source, destination);

    /// <inheritdoc />
    public override void FromAbgr32(Configuration configuration, ReadOnlySpan<Abgr32> source, Span<TPixel> destination)
        => FromByteSource(configuration, source, destination);

    /// <inheritdoc />
    public override void FromBgr24(Configuration configuration, ReadOnlySpan<Bgr24> source, Span<TPixel> destination)
        => FromByteSource(configuration, source, destination);

    /// <inheritdoc />
    public override void FromL8(Configuration configuration, ReadOnlySpan<L8> source, Span<TPixel> destination)
        => FromByteSource(configuration, source, destination);

    /// <inheritdoc />
    public override void FromLa16(Configuration configuration, ReadOnlySpan<La16> source, Span<TPixel> destination)
        => FromByteSource(configuration, source, destination);

    /// <summary>
    /// Converts 16-bit source pixels through the <see cref="Rgba64"/> source operator of their layout.
    /// </summary>
    /// <typeparam name="TSource">The source operator.</typeparam>
    /// <typeparam name="TSourcePixel">The source pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination pixels.</param>
    private static void FromRgba64Source<TSource, TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
        where TSource : PixelConverter.IRgba64SourceOperator
        where TSourcePixel : unmanaged
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ReadOnlySpan<byte> sourceBytes = MemoryMarshal.AsBytes(source);
        Span<byte> destinationBytes = MemoryMarshal.AsBytes(destination);

        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            PixelConverter.Rgba64SourceKernels<TSource>.ToRgba64(sourceBytes, destinationBytes);
        }
        else
        {
            PixelConverter.Rgba64SourceKernels<TSource>.ToRgb48(sourceBytes, destinationBytes);
        }
    }

    /// <summary>
    /// Converts 8-bit source pixels: the source converts to the 8-bit layout of the destination channel order, then
    /// each byte scales to 16 bits.
    /// </summary>
    /// <remarks>
    /// An <see cref="Rgba32"/> pixel is half the size of an <see cref="Rgba64"/> pixel, and an <see cref="Rgb24"/> pixel is
    /// half the size of an <see cref="Rgb48"/> pixel. Thus the source converts to <see cref="Rgba32"/> or
    /// <see cref="Rgb24"/> in the second half of the destination storage, through the bulk conversion of the source
    /// type. <see cref="PixelConverter.ScaleUnorm8To16(ReadOnlySpan{byte}, Span{ushort})"/> then scales those bytes
    /// forward to the full storage. No temporary buffer is necessary.
    /// </remarks>
    /// <typeparam name="TSourcePixel">The 8-bit source pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination pixels.</param>
    private static void FromByteSource<TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
        where TSourcePixel : unmanaged, IPixel<TSourcePixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Span<TPixel> pixels = destination[..source.Length];
        Span<byte> storage = MemoryMarshal.AsBytes(pixels);
        Span<byte> staging = storage[(storage.Length / 2)..];

        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            PixelOperations<TSourcePixel>.Instance.ToRgba32(configuration, source, MemoryMarshal.Cast<byte, Rgba32>(staging));
        }
        else
        {
            PixelOperations<TSourcePixel>.Instance.ToRgb24(configuration, source, MemoryMarshal.Cast<byte, Rgb24>(staging));
        }

        PixelConverter.ScaleUnorm8To16(staging, MemoryMarshal.Cast<TPixel, ushort>(pixels));
    }

    /// <summary>
    /// Converts complete pixel vectors to RGB or RGBA words in one pass without temporary storage.
    /// </summary>
    /// <param name="source">The normalized input vectors.</param>
    /// <param name="destination">Storage for at least the source pixel count.</param>
    /// <param name="associated">Whether RGB requires division by alpha before packing.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void Pack(ReadOnlySpan<Vector4> source, Span<TPixel> destination, bool associated)
    {
        ReadOnlySpan<float> components = MemoryMarshal.Cast<Vector4, float>(source);
        ref float input = ref MemoryMarshal.GetReference(components);
        ref ushort output = ref Unsafe.As<TPixel, ushort>(ref MemoryMarshal.GetReference(destination));
        int componentCount = TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount;
        int i = 0;

        // Two float registers become one word register: eight pixels at 512 bits, four at 256, or two at 128.
        // Each float register holds complete RGBA groups. Layout stores retain alpha for RGBA and remove it for RGB.
        // Read the component count before traversal. The caller supplies a fixed association mode for the inlined kernel.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector512Count<float>() / 2;

            for (nuint block = 0; block < count; block++, i += Vector512<ushort>.Count)
            {
                Vector512<float> lower = Vector512.LoadUnsafe(ref input, (uint)i);
                Vector512<float> upper = Vector512.LoadUnsafe(ref input, (uint)(i + Vector512<float>.Count));

                // Unassociate before destination bounds. This also preserves the single-pixel rule for zero or tiny alpha.
                if (associated)
                {
                    lower = PixelFormats.PixelOperations.UnassociateOperator.Apply(lower);
                    upper = PixelFormats.PixelOperations.UnassociateOperator.Apply(upper);
                }

                Vector512<ushort> packed = Vector512.Create(
                    SimdUtils.NormalizedFloatToUInt16Saturate(lower),
                    SimdUtils.NormalizedFloatToUInt16Saturate(upper));

                // i counts input float components. Convert to pixels, then to destination words without row padding.
                nuint offset = ((nuint)i / 4) * (uint)componentCount;
                PixelFormats.PixelOperations.Store16BitComponents(packed, ref output, offset, componentCount);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector256Count<float>() / 2;

            for (nuint block = 0; block < count; block++, i += Vector256<ushort>.Count)
            {
                Vector256<float> lower = Vector256.LoadUnsafe(ref input, (uint)i);
                Vector256<float> upper = Vector256.LoadUnsafe(ref input, (uint)(i + Vector256<float>.Count));

                // Unassociate before destination bounds. This also preserves the single-pixel rule for zero or tiny alpha.
                if (associated)
                {
                    lower = PixelFormats.PixelOperations.UnassociateOperator.Apply(lower);
                    upper = PixelFormats.PixelOperations.UnassociateOperator.Apply(upper);
                }

                Vector256<ushort> packed = Vector256.Create(
                    SimdUtils.NormalizedFloatToUInt16Saturate(lower),
                    SimdUtils.NormalizedFloatToUInt16Saturate(upper));

                // i counts input float components. Convert to pixels, then to destination words without row padding.
                nuint offset = ((nuint)i / 4) * (uint)componentCount;
                PixelFormats.PixelOperations.Store16BitComponents(packed, ref output, offset, componentCount);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector128Count<float>() / 2;

            for (nuint block = 0; block < count; block++, i += Vector128<ushort>.Count)
            {
                Vector128<float> lower = Vector128.LoadUnsafe(ref input, (uint)i);
                Vector128<float> upper = Vector128.LoadUnsafe(ref input, (uint)(i + Vector128<float>.Count));

                // Unassociate before destination bounds. This also preserves the single-pixel rule for zero or tiny alpha.
                if (associated)
                {
                    lower = PixelFormats.PixelOperations.UnassociateOperator.Apply(lower);
                    upper = PixelFormats.PixelOperations.UnassociateOperator.Apply(upper);
                }

                Vector128<ushort> packed = Vector128.Create(
                    SimdUtils.NormalizedFloatToUInt16Saturate(lower),
                    SimdUtils.NormalizedFloatToUInt16Saturate(upper));

                // i counts input float components. Convert to pixels, then to destination words without row padding.
                nuint offset = ((nuint)i / 4) * (uint)componentCount;
                PixelFormats.PixelOperations.Store16BitComponents(packed, ref output, offset, componentCount);
            }
        }

        // Every vector loop ends on a pixel boundary. The final pixel uses the same public conversion contract.
        for (int pixel = i / 4; pixel < source.Length; pixel++)
        {
            destination[pixel] = associated
                ? TPixel.FromAssociatedScaledVector4(source[pixel])
                : TPixel.FromUnassociatedScaledVector4(source[pixel]);
        }
    }

    /// <summary>
    /// Expands packed RGB words to pixel vectors with opaque alpha.
    /// </summary>
    /// <param name="configuration">The conversion configuration.</param>
    /// <param name="source">The RGB48-layout pixels.</param>
    /// <param name="destination">The output vectors.</param>
    private static void UnpackRgb48(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ReadOnlySpan<ushort> packed = MemoryMarshal.Cast<TPixel, ushort>(source);
        ref float output = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        // Every ushort fits in a positive int. Use signed conversion so older SIMD targets need no uint conversion emulation.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector512Count<float>();
            Vector512<float> maximum = Vector512.Create((float)ushort.MaxValue);

            // Each register holds one component from 16 pixels. Divide before adding opaque alpha, then pack complete pixel vectors.
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

            // Each register holds one component from 8 pixels. Divide before adding opaque alpha, then pack complete pixel vectors.
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

            // Each register holds one component from 4 pixels. Divide before adding opaque alpha, then pack complete pixel vectors.
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
}

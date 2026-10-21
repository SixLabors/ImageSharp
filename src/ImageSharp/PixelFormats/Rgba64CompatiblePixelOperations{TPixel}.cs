// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

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
        this.ToUnassociatedVector4(configuration, source, destination);

        // Apply stored straight alpha. Formats without alpha already supply opaque output.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
        {
            Numerics.Premultiply(destination[..source.Length]);
        }
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

    /// <summary>
    /// Converts complete RGBA vectors to RGB or RGBA words in one pass without temporary storage.
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
                    lower = PixelFormats.PixelOperations.UnPremultiplyForPacking(lower);
                    upper = PixelFormats.PixelOperations.UnPremultiplyForPacking(upper);
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
                    lower = PixelFormats.PixelOperations.UnPremultiplyForPacking(lower);
                    upper = PixelFormats.PixelOperations.UnPremultiplyForPacking(upper);
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
                    lower = PixelFormats.PixelOperations.UnPremultiplyForPacking(lower);
                    upper = PixelFormats.PixelOperations.UnPremultiplyForPacking(upper);
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
    /// Expands packed RGB words to RGBA vectors with opaque alpha.
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
}

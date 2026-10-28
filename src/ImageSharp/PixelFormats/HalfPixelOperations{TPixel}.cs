// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares bulk conversion for half-precision pixel storage.
/// </summary>
/// <remarks>
/// One-, two-, and four-component layouts share unpacking, component bounds and alpha conversion in one traversal.
/// Native vectors retain IEEE values. Normalized colors use the shared finite RGB and alpha bounds.
/// Packing associates straight input using the alpha rounded to destination storage; associated input is not rescaled.
/// </remarks>
/// <typeparam name="TPixel">The half-precision pixel format.</typeparam>
internal abstract class HalfPixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <inheritdoc />
    public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]),
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Unassociated);
        }
        else
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]),
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Unassociated);
        }
    }

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]),
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Associated);
        }
        else
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]),
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Associated);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                destination[..source.Length],
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Unassociated);
        }
        else
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                destination[..source.Length],
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Unassociated);
        }
    }

    /// <inheritdoc />
    public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                destination[..source.Length],
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Associated);
        }
        else
        {
            Unpack(
                MemoryMarshal.Cast<TPixel, ushort>(source),
                destination[..source.Length],
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Associated);
        }
    }

    /// <inheritdoc />
    public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Associated);
        }
        else
        {
            Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Unassociated);
        }
    }

    /// <inheritdoc />
    public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Associated);
        }
        else
        {
            Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Unassociated);
        }
    }

    /// <inheritdoc />
    public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Pack(
                source,
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Associated);
        }
        else
        {
            Pack(
                source,
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Unassociated,
                PixelAlphaRepresentation.Unassociated);
        }
    }

    /// <inheritdoc />
    public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Select stored alpha once. Literal arguments let the inlined kernel remove unused conversion branches.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Pack(
                source,
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Associated);
        }
        else
        {
            Pack(
                source,
                MemoryMarshal.Cast<TPixel, ushort>(destination[..source.Length]),
                PixelAlphaRepresentation.Associated,
                PixelAlphaRepresentation.Unassociated);
        }
    }

    /// <summary>
    /// Expands stored half-precision components to native pixel vectors with the requested alpha representation.
    /// </summary>
    /// <param name="source">The stored R, RG, or RGBA components.</param>
    /// <param name="destination">Caller-owned storage for four output components per pixel.</param>
    /// <param name="sourceAlpha">The alpha representation of the source components.</param>
    /// <param name="destinationAlpha">The alpha representation required by the destination.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void Unpack(
        ReadOnlySpan<ushort> source,
        Span<float> destination,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha)
        => UnpackRgba(source, destination, sourceAlpha, destinationAlpha, false);

    /// <summary>
    /// Expands stored half-precision components to normalized pixel vectors with the requested alpha representation.
    /// </summary>
    /// <param name="source">The stored R, RG, or RGBA components.</param>
    /// <param name="destination">Caller-owned destination storage.</param>
    /// <param name="sourceAlpha">The alpha representation of the source components.</param>
    /// <param name="destinationAlpha">The alpha representation required by the destination.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void Unpack(
        ReadOnlySpan<ushort> source,
        Span<Vector4> destination,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha)
        => UnpackRgba(source, MemoryMarshal.Cast<Vector4, float>(destination), sourceAlpha, destinationAlpha, true);

    /// <summary>
    /// Expands stored half-precision pixels to pixel vectors in one pass with no intermediate row.
    /// </summary>
    /// <param name="source">The stored R, RG, or RGBA components.</param>
    /// <param name="destination">Four output components per source pixel.</param>
    /// <param name="sourceAlpha">The source alpha representation.</param>
    /// <param name="destinationAlpha">The destination alpha representation.</param>
    /// <param name="normalized">Whether the pixel color API requires finite RGB and opacity bounds.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void UnpackRgba(
        ReadOnlySpan<ushort> source,
        Span<float> destination,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha,
        bool normalized)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int componentCount = TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount;
        int i = 0;

        // Pixel metadata identifies formats with implicit opaque alpha. Association cannot change their components.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.None)
        {
            destinationAlpha = sourceAlpha;
        }

        // Count output lanes, not stored components: each pixel always writes RGBA. Layout loads read only
        // the stored R, RG, or RGBA components and insert empty slots before half conversion.
        // A word register expands into two float registers: eight pixels at 512 bits, four at 256, or two at 128.
        // The shared index counts float components. Dividing by 4/componentCount converts it to a stored-word offset.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = destination[i..].Vector512Count<float>() / 2;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                nuint sourceOffset = (nuint)i / (uint)(4 / componentCount);
                PixelFormats.PixelOperations.Load16BitComponents(ref sourceBase, sourceOffset, componentCount, out Vector512<ushort> packed);
                (Vector512<float> lower, Vector512<float> upper) = HalfTypeHelper.Unpack(packed);

                // Restore missing channels before alpha arithmetic. R and RG supply B = 0 and opaque alpha.
                lower = PixelFormats.PixelOperations.RestoreImplicitComponents(lower, componentCount);
                upper = PixelFormats.PixelOperations.RestoreImplicitComponents(upper, componentCount);
                Vector512.StoreUnsafe(ConvertAlpha(lower, sourceAlpha, destinationAlpha, normalized, false), ref destinationBase, (nuint)i);
                upper = ConvertAlpha(upper, sourceAlpha, destinationAlpha, normalized, false);
                Vector512.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector512<float>.Count));
            }
        }

        // Continue at the first unconverted pixel. Smaller registers never read beyond the remaining complete pixels.
        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = destination[i..].Vector256Count<float>() / 2;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                nuint sourceOffset = (nuint)i / (uint)(4 / componentCount);
                PixelFormats.PixelOperations.Load16BitComponents(ref sourceBase, sourceOffset, componentCount, out Vector256<ushort> packed);
                (Vector256<float> lower, Vector256<float> upper) = HalfTypeHelper.Unpack(packed);
                lower = PixelFormats.PixelOperations.RestoreImplicitComponents(lower, componentCount);
                upper = PixelFormats.PixelOperations.RestoreImplicitComponents(upper, componentCount);
                Vector256.StoreUnsafe(ConvertAlpha(lower, sourceAlpha, destinationAlpha, normalized, false), ref destinationBase, (nuint)i);
                upper = ConvertAlpha(upper, sourceAlpha, destinationAlpha, normalized, false);
                Vector256.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector256<float>.Count));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = destination[i..].Vector128Count<float>() / 2;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                nuint sourceOffset = (nuint)i / (uint)(4 / componentCount);
                PixelFormats.PixelOperations.Load16BitComponents(ref sourceBase, sourceOffset, componentCount, out Vector128<ushort> packed);
                (Vector128<float> lower, Vector128<float> upper) = HalfTypeHelper.Unpack(packed);
                lower = PixelFormats.PixelOperations.RestoreImplicitComponents(lower, componentCount);
                upper = PixelFormats.PixelOperations.RestoreImplicitComponents(upper, componentCount);
                Vector128.StoreUnsafe(ConvertAlpha(lower, sourceAlpha, destinationAlpha, normalized, false), ref destinationBase, (nuint)i);
                upper = ConvertAlpha(upper, sourceAlpha, destinationAlpha, normalized, false);
                Vector128.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector128<float>.Count));
            }
        }

        // The tail reads one stored pixel and writes all four output lanes, including the same implicit defaults.
        for (; i < destination.Length; i += 4)
        {
            int sourceIndex = i / (4 / componentCount);
            Vector4 value = new(
                HalfTypeHelper.Unpack(source[sourceIndex]),
                componentCount > 1 ? HalfTypeHelper.Unpack(source[sourceIndex + 1]) : 0F,
                componentCount == 4 ? HalfTypeHelper.Unpack(source[sourceIndex + 2]) : 0F,
                componentCount == 4 ? HalfTypeHelper.Unpack(source[sourceIndex + 3]) : 1F);

            value = ConvertAlpha(value.AsVector128(), sourceAlpha, destinationAlpha, normalized, false).AsVector4();

            destination[i + 0] = value.X;
            destination[i + 1] = value.Y;
            destination[i + 2] = value.Z;
            destination[i + 3] = value.W;
        }
    }

    /// <summary>
    /// Packs native pixel vectors into half-precision pixel storage with the requested alpha representation.
    /// </summary>
    /// <param name="source">The source components in RGBA order.</param>
    /// <param name="destination">Caller-owned R, RG, or RGBA storage.</param>
    /// <param name="sourceAlpha">The alpha representation of the source components.</param>
    /// <param name="destinationAlpha">The alpha representation required by the destination.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void Pack(
        ReadOnlySpan<float> source,
        Span<ushort> destination,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha)
        => PackRgba(source, destination, sourceAlpha, destinationAlpha, false);

    /// <summary>
    /// Packs normalized pixel vectors into half-precision pixel storage with the requested alpha representation.
    /// </summary>
    /// <param name="source">The source components in RGBA order.</param>
    /// <param name="destination">Caller-owned R, RG, or RGBA storage.</param>
    /// <param name="sourceAlpha">The alpha representation of the source components.</param>
    /// <param name="destinationAlpha">The alpha representation required by the destination.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void Pack(
        ReadOnlySpan<Vector4> source,
        Span<ushort> destination,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha)
        => PackRgba(MemoryMarshal.Cast<Vector4, float>(source), destination, sourceAlpha, destinationAlpha, true);

    /// <summary>
    /// Packs RGBA components in one pass with no intermediate row.
    /// </summary>
    /// <param name="source">The complete RGBA component groups.</param>
    /// <param name="destination">R, RG, or RGBA storage for each source pixel.</param>
    /// <param name="sourceAlpha">The source alpha representation.</param>
    /// <param name="destinationAlpha">The destination alpha representation.</param>
    /// <param name="normalized">Whether the pixel color API requires finite RGB and opacity bounds.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void PackRgba(
        ReadOnlySpan<float> source,
        Span<ushort> destination,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha,
        bool normalized)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        int componentCount = TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount;
        int i = 0;

        // Each pack consumes two float registers and produces one ushort register.
        // Layout stores then remove absent components. Four-component formats use a direct register store.
        // Read the component count before traversal. Layout stores retain only the components declared by pixel metadata.
        // The input index counts RGBA components. Divide by 4/componentCount to locate the packed destination pixel.
        // Loads and stores allow unaligned addresses. Their counts exclude partial registers at the end of either span.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector512Count<float>() / 2;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                Vector512<float> lower = ConvertAlpha(Vector512.LoadUnsafe(ref sourceBase, (nuint)i), sourceAlpha, destinationAlpha, normalized, true);
                Vector512<float> upper = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count));
                upper = ConvertAlpha(upper, sourceAlpha, destinationAlpha, normalized, true);
                Vector512<ushort> packed = HalfTypeHelper.Pack(lower, upper);
                nuint destinationOffset = (nuint)i / (uint)(4 / componentCount);
                PixelFormats.PixelOperations.Store16BitComponents(packed, ref destinationBase, destinationOffset, componentCount);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector256Count<float>() / 2;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                Vector256<float> lower = ConvertAlpha(Vector256.LoadUnsafe(ref sourceBase, (nuint)i), sourceAlpha, destinationAlpha, normalized, true);
                Vector256<float> upper = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count));
                upper = ConvertAlpha(upper, sourceAlpha, destinationAlpha, normalized, true);
                Vector256<ushort> packed = HalfTypeHelper.Pack(lower, upper);
                nuint destinationOffset = (nuint)i / (uint)(4 / componentCount);
                PixelFormats.PixelOperations.Store16BitComponents(packed, ref destinationBase, destinationOffset, componentCount);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector128Count<float>() / 2;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                Vector128<float> lower = ConvertAlpha(Vector128.LoadUnsafe(ref sourceBase, (nuint)i), sourceAlpha, destinationAlpha, normalized, true);
                Vector128<float> upper = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count));
                upper = ConvertAlpha(upper, sourceAlpha, destinationAlpha, normalized, true);
                Vector128<ushort> packed = HalfTypeHelper.Pack(lower, upper);
                nuint destinationOffset = (nuint)i / (uint)(4 / componentCount);
                PixelFormats.PixelOperations.Store16BitComponents(packed, ref destinationBase, destinationOffset, componentCount);
            }
        }

        // The remainder uses the same arithmetic and rounds only the components stored by the destination.
        for (; i < source.Length; i += 4)
        {
            Vector4 value = new(source[i + 0], source[i + 1], source[i + 2], source[i + 3]);
            value = ConvertAlpha(value.AsVector128(), sourceAlpha, destinationAlpha, normalized, true).AsVector4();

            int destinationIndex = i / (4 / componentCount);
            destination[destinationIndex] = HalfTypeHelper.Pack(value.X);

            if (componentCount > 1)
            {
                destination[destinationIndex + 1] = HalfTypeHelper.Pack(value.Y);
            }

            if (componentCount == 4)
            {
                destination[destinationIndex + 2] = HalfTypeHelper.Pack(value.Z);
                destination[destinationIndex + 3] = HalfTypeHelper.Pack(value.W);
            }
        }
    }

    /// <summary>
    /// Converts RGBA alpha representation before a half load or store completes.
    /// </summary>
    /// <param name="value">The complete RGBA groups.</param>
    /// <param name="sourceAlpha">The source alpha representation.</param>
    /// <param name="destinationAlpha">The destination alpha representation.</param>
    /// <param name="normalized">Whether the pixel color API requires finite component bounds.</param>
    /// <param name="roundAlpha">Whether association must use the alpha retained by half storage.</param>
    /// <returns>The converted RGBA components.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector128<float> ConvertAlpha(
        Vector128<float> value,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha,
        bool normalized,
        bool roundAlpha)
    {
        // Matching representations preserve associated RGB. Storage rounding alone never rescales RGB.
        if (sourceAlpha == destinationAlpha)
        {
            return normalized ? PixelFormats.PixelOperations.ClampToPixelRange(value) : value;
        }

        if (sourceAlpha == PixelAlphaRepresentation.Associated)
        {
            // Color conversion bounds the quotient after division. Native conversion retains IEEE arithmetic.
            return normalized
                ? PixelFormats.PixelOperations.UnPremultiply(value)
                : Numerics.UnPremultiply(value, Vector128_.ShuffleNative(value, 0b_11_11_11_11));
        }

        if (normalized)
        {
            value = PixelFormats.PixelOperations.ClampToPixelRange(value);
        }

        // Each RGBA group repeats its own alpha. Restore W after multiplication so alpha is not squared.
        Vector128<float> alpha = Vector128_.ShuffleNative(value, 0b_11_11_11_11);

        // Round opacity here, where the destination's half storage precision is known.
        if (roundAlpha)
        {
            alpha = HalfTypeHelper.RoundToHalf(alpha);
        }

        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();

        return Vector128.ConditionalSelect(alphaMask, alpha, value * alpha);
    }

    /// <summary>
    /// Converts RGBA alpha representation before a half load or store completes.
    /// </summary>
    /// <param name="value">The complete RGBA groups.</param>
    /// <param name="sourceAlpha">The source alpha representation.</param>
    /// <param name="destinationAlpha">The destination alpha representation.</param>
    /// <param name="normalized">Whether the pixel color API requires finite component bounds.</param>
    /// <param name="roundAlpha">Whether association must use the alpha retained by half storage.</param>
    /// <returns>The converted RGBA components.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector256<float> ConvertAlpha(
        Vector256<float> value,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha,
        bool normalized,
        bool roundAlpha)
    {
        // Matching representations preserve associated RGB. Storage rounding alone never rescales RGB.
        if (sourceAlpha == destinationAlpha)
        {
            return normalized ? PixelFormats.PixelOperations.ClampToPixelRange(value) : value;
        }

        if (sourceAlpha == PixelAlphaRepresentation.Associated)
        {
            // Color conversion bounds the quotient after division. Native conversion retains IEEE arithmetic.
            return normalized
                ? PixelFormats.PixelOperations.UnPremultiply(value)
                : Numerics.UnPremultiply(value, Vector256_.ShuffleNative(value, 0b_11_11_11_11));
        }

        if (normalized)
        {
            value = PixelFormats.PixelOperations.ClampToPixelRange(value);
        }

        // Each RGBA group repeats its own alpha. Restore W after multiplication so alpha is not squared.
        Vector256<float> alpha = Vector256_.ShuffleNative(value, 0b_11_11_11_11);

        // Round opacity here, where the destination's half storage precision is known.
        if (roundAlpha)
        {
            alpha = HalfTypeHelper.RoundToHalf(alpha);
        }

        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

        return Vector256.ConditionalSelect(alphaMask, alpha, value * alpha);
    }

    /// <summary>
    /// Converts RGBA alpha representation before a half load or store completes.
    /// </summary>
    /// <param name="value">The complete RGBA groups.</param>
    /// <param name="sourceAlpha">The source alpha representation.</param>
    /// <param name="destinationAlpha">The destination alpha representation.</param>
    /// <param name="normalized">Whether the pixel color API requires finite component bounds.</param>
    /// <param name="roundAlpha">Whether association must use the alpha retained by half storage.</param>
    /// <returns>The converted RGBA components.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector512<float> ConvertAlpha(
        Vector512<float> value,
        PixelAlphaRepresentation sourceAlpha,
        PixelAlphaRepresentation destinationAlpha,
        bool normalized,
        bool roundAlpha)
    {
        // Matching representations preserve associated RGB. Storage rounding alone never rescales RGB.
        if (sourceAlpha == destinationAlpha)
        {
            return normalized ? PixelFormats.PixelOperations.ClampToPixelRange(value) : value;
        }

        if (sourceAlpha == PixelAlphaRepresentation.Associated)
        {
            // Color conversion bounds the quotient after division. Native conversion retains IEEE arithmetic.
            return normalized
                ? PixelFormats.PixelOperations.UnPremultiply(value)
                : Numerics.UnPremultiply(value, Vector512_.ShuffleNative(value, 0b_11_11_11_11));
        }

        if (normalized)
        {
            value = PixelFormats.PixelOperations.ClampToPixelRange(value);
        }

        // Each RGBA group repeats its own alpha. Restore W after multiplication so alpha is not squared.
        Vector512<float> alpha = Vector512_.ShuffleNative(value, 0b_11_11_11_11);

        // Round opacity here, where the destination's half storage precision is known.
        if (roundAlpha)
        {
            alpha = HalfTypeHelper.RoundToHalf(alpha);
        }

        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

        return Vector512.ConditionalSelect(alphaMask, alpha, value * alpha);
    }
}

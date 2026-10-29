// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides bulk conversion operations for pixel buffers of type <typeparamref name="TPixel"/>.
/// </summary>
/// <remarks>
/// Default bulk conversions follow the scalar pixel API. Native vector methods select the format's declared alpha representation.
/// Bulk vector conversions follow the corresponding <see cref="IPixel{TSelf}"/> scalar conversion rules.
/// Integer destinations clamp components and map NaN to the lower endpoint of their numeric range.
/// Floating-point color conversions bound RGB to [-65504,65504] and alpha to [0,1]. Native vector conversions retain IEEE special values.
/// Finite HDR values remain unchanged within this color range. In-range signed zero is preserved.
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public partial class PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    // PixelOperations owns pixel-format conversion rules and bulk optimizations, including those used by shared blenders.
    // Storage-family overrides share optimized packing and SIMD traversal. Pixel formats own rounding and storage layout.
    // PixelBlenders selects composition equations; PixelBlender applies them through these same conversion APIs.
    // Keeping selection outside PixelOperations prevents pixel conversion from referencing unrelated blend equations.

    /// <summary>
    /// Gets the shared <see cref="PixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <exception cref="TypeInitializationException">The pixel type's factory could not create the shared instance.</exception>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static PixelOperations<TPixel> Instance => InstanceHolder.Value;
#pragma warning restore CA1000 // Do not declare static members on generic types

#pragma warning disable CA1000 // Scalar conversions share the public generic owner with bulk conversions.
    /// <summary>
    /// Bounds normalized floating-point RGB to [-65504,65504] and alpha to [0,1] before pixel storage.
    /// </summary>
    /// <param name="source">The color in normalized component units.</param>
    /// <returns>The bounded color. NaN maps to the lower bound; infinities map to the corresponding bounds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ClampToPixelRange(Vector4 source) => PixelFormats.PixelOperations.ClampToPixelRange(source.AsVector128()).AsVector4();

    /// <summary>
    /// Restores straight RGB and bounds normalized color components before destination packing.
    /// </summary>
    /// <param name="source">The associated color in normalized component units.</param>
    /// <remarks>
    /// NaN and negative alpha become zero; positive infinity becomes <see cref="float.MaxValue"/>.
    /// The divisor retains positive finite alpha, including values above one. Zero alpha retains stored RGB.
    /// After division, RGB is bounded to [-65504,65504] and alpha to [0,1]. This also bounds overflow from tiny positive alpha.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnPremultiply(ref Vector4 source) => source = PixelFormats.PixelOperations.UnPremultiply(source.AsVector128()).AsVector4();

    /// <summary>
    /// Converts associated normalized input through the destination's straight-color pixel conversion.
    /// </summary>
    /// <param name="source">The associated color in normalized component units.</param>
    /// <returns>The pixel after alpha restoration and destination packing.</returns>
    /// <remarks>
    /// This default is for destinations that store straight RGB or have no alpha channel.
    /// Associated destinations retain their own storage conversion to preserve associated RGB and alpha rounding.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel FromAssociatedScaledVector4(Vector4 source)
    {
        // Destination packing applies its own bounds after division. Clamping to floating-point bounds here
        // would add work before SDR packing and could change grayscale calculations before luminance is known.
        source = PixelFormats.PixelOperations.UnPremultiplyForPacking(source.AsVector128()).AsVector4();

        return TPixel.FromUnassociatedScaledVector4(source);
    }
#pragma warning restore CA1000 // Do not declare static members on generic types

    /// <summary>
    /// Converts pixels to vectors in the pixel format's native numeric range with unassociated alpha.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
        ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = sourceStart.ToUnassociatedVector4();

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts pixels to vectors in the pixel format's native numeric range with associated alpha.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
        ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = sourceStart.ToAssociatedVector4();

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts pixels to scaled vectors with unassociated alpha.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
        ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = sourceStart.ToUnassociatedScaledVector4();

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts pixels to scaled vectors with associated alpha.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
        ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = sourceStart.ToAssociatedScaledVector4();

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts native-range vectors with unassociated alpha to pixels. The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
        ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = TPixel.FromUnassociatedVector4(sourceStart);

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts native-range vectors with associated alpha to pixels. The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
        ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = TPixel.FromAssociatedVector4(sourceStart);

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts scaled vectors with unassociated alpha to pixels. The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
        ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = TPixel.FromUnassociatedScaledVector4(sourceStart);

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts scaled vectors with associated alpha to pixels. The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
        ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
        ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

        while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
        {
            destinationBase = TPixel.FromAssociatedScaledVector4(sourceStart);

            sourceStart = ref Unsafe.Add(ref sourceStart, 1);
            destinationBase = ref Unsafe.Add(ref destinationBase, 1);
        }
    }

    /// <summary>
    /// Converts pixels to vectors in the native numeric range and with their native alpha representation.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        // The native API uses the format's stored alpha representation. Explicit APIs select their representation directly.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.ToAssociatedVector4(configuration, source, destination);
        }
        else
        {
            this.ToUnassociatedVector4(configuration, source, destination);
        }
    }

    /// <summary>
    /// Converts pixels to scaled vectors with their native alpha representation.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        // The native API uses the format's stored alpha representation. Explicit APIs select their representation directly.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.ToAssociatedScaledVector4(configuration, source, destination);
        }
        else
        {
            this.ToUnassociatedScaledVector4(configuration, source, destination);
        }
    }

    /// <summary>
    /// Converts vectors in the native numeric range and with the pixel format's native alpha representation to pixels.
    /// The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        // The native API uses the format's stored alpha representation. Explicit APIs select their representation directly.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.FromAssociatedVector4Destructive(configuration, source, destination);
        }
        else
        {
            this.FromUnassociatedVector4Destructive(configuration, source, destination);
        }
    }

    /// <summary>
    /// Converts scaled vectors with the pixel format's native alpha representation to pixels. The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        // The native API uses the format's stored alpha representation. Explicit APIs select their representation directly.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.FromAssociatedScaledVector4Destructive(configuration, source, destination);
        }
        else
        {
            this.FromUnassociatedScaledVector4Destructive(configuration, source, destination);
        }
    }

    /// <summary>
    /// Gets the pixel type info for the given <typeparamref name="TPixel"/>.
    /// </summary>
    /// <returns>The <see cref="PixelTypeInfo"/>.</returns>
    public PixelTypeInfo GetPixelTypeInfo() => TPixel.GetPixelTypeInfo();

    /// <summary>
    /// Converts unassociated scaled planar floating-point components to a contiguous row of pixels.
    /// An empty fourth plane supplies a value of 1 for each pixel.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="component0">The first component plane.</param>
    /// <param name="component1">The second component plane.</param>
    /// <param name="component2">The third component plane.</param>
    /// <param name="component3">The fourth component plane, or an empty span to use 1 for every value.</param>
    /// <param name="destination">The destination pixels.</param>
    internal void PackFromUnassociatedScaledFloatPlanes(
        Configuration configuration,
        ReadOnlySpan<float> component0,
        ReadOnlySpan<float> component1,
        ReadOnlySpan<float> component2,
        ReadOnlySpan<float> component3,
        Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.IsTrue(component1.Length == component0.Length, nameof(component1), "Components must be of same size!");
        Guard.IsTrue(component2.Length == component0.Length, nameof(component2), "Components must be of same size!");
        Guard.IsTrue(component3.IsEmpty || component3.Length == component0.Length, nameof(component3), "Components must be of same size!");
        Guard.DestinationShouldNotBeTooShort(component0, destination, nameof(destination));

        const int BlockSize = 64;
        InlineArray64<Vector4> buffer = default;
        Span<Vector4> vectors = buffer;

        // Use the same bounded block as associated packing. Interleave straight samples once, then let the destination pixel operation apply its alpha rules.
        for (int offset = 0; offset < component0.Length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, component0.Length - offset);
            Span<Vector4> block = vectors[..count];
            SimdUtils.InterleaveFloatPlanes(
                component0.Slice(offset, count),
                component1.Slice(offset, count),
                component2.Slice(offset, count),
                component3.IsEmpty ? ReadOnlySpan<float>.Empty : component3.Slice(offset, count),
                block);

            this.FromUnassociatedScaledVector4Destructive(configuration, block, destination.Slice(offset, count));
        }
    }

    /// <summary>
    /// Converts associated scaled planar floating-point components to a contiguous row of pixels.
    /// An empty fourth plane supplies a value of 1 for each pixel.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="component0">The first component plane.</param>
    /// <param name="component1">The second component plane.</param>
    /// <param name="component2">The third component plane.</param>
    /// <param name="component3">The fourth component plane, or an empty span to use 1 for every value.</param>
    /// <param name="destination">The destination pixels.</param>
    internal void PackFromAssociatedScaledFloatPlanes(
        Configuration configuration,
        ReadOnlySpan<float> component0,
        ReadOnlySpan<float> component1,
        ReadOnlySpan<float> component2,
        ReadOnlySpan<float> component3,
        Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.IsTrue(component1.Length == component0.Length, nameof(component1), "Components must be of same size!");
        Guard.IsTrue(component2.Length == component0.Length, nameof(component2), "Components must be of same size!");
        Guard.IsTrue(component3.IsEmpty || component3.Length == component0.Length, nameof(component3), "Components must be of same size!");
        Guard.DestinationShouldNotBeTooShort(component0, destination, nameof(destination));

        const int BlockSize = 64;
        InlineArray64<Vector4> buffer = default;
        Span<Vector4> vectors = buffer;

        // The transposer and pixel converter share one bounded scratch block. Each pixel is
        // converted once, and the final short block follows the same numeric-domain path.
        for (int offset = 0; offset < component0.Length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, component0.Length - offset);
            Span<Vector4> block = vectors[..count];
            SimdUtils.InterleaveFloatPlanes(
                component0.Slice(offset, count),
                component1.Slice(offset, count),
                component2.Slice(offset, count),
                component3.IsEmpty ? ReadOnlySpan<float>.Empty : component3.Slice(offset, count),
                block);

            this.FromAssociatedScaledVector4Destructive(configuration, block, destination.Slice(offset, count));
        }
    }

    /// <summary>
    /// Converts a contiguous row of pixels to associated scaled planar floating-point components.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="component0">The first component plane.</param>
    /// <param name="component1">The second component plane.</param>
    /// <param name="component2">The third component plane.</param>
    /// <param name="component3">The fourth component plane.</param>
    internal void UnpackToAssociatedScaledFloatPlanes(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        Span<float> component3)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, component0, nameof(component0));
        Guard.DestinationShouldNotBeTooShort(source, component1, nameof(component1));
        Guard.DestinationShouldNotBeTooShort(source, component2, nameof(component2));
        Guard.DestinationShouldNotBeTooShort(source, component3, nameof(component3));

        const int BlockSize = 64;
        Span<Vector4> vectors = stackalloc Vector4[BlockSize];

        for (int offset = 0; offset < source.Length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, source.Length - offset);
            Span<Vector4> block = vectors[..count];
            this.ToAssociatedScaledVector4(configuration, source.Slice(offset, count), block);
            SimdUtils.DeinterleaveFloatPlanes(
                block,
                component0.Slice(offset, count),
                component1.Slice(offset, count),
                component2.Slice(offset, count),
                component3.Slice(offset, count));
        }
    }

    /// <summary>
    /// Bulk operation that converts <paramref name="source"/> pixels from <typeparamref name="TPixelFrom"/> format to
    /// <typeparamref name="TPixel"/> destination pixels.
    /// </summary>
    /// <typeparam name="TPixelFrom">The source pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TPixelFrom}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TPixel}"/> to the destination pixels.</param>
    public virtual void From<TPixelFrom>(
        Configuration configuration,
        ReadOnlySpan<TPixelFrom> source,
        Span<TPixel> destination)
        where TPixelFrom : unmanaged, IPixel<TPixelFrom>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (source.IsEmpty)
        {
            return;
        }

        int sliceLength = Math.Min(source.Length, 1024);
        int numberOfSlices = source.Length / sliceLength;

        using IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(sliceLength);
        Span<Vector4> vectorSpan = tempVectors.GetSpan()[..sliceLength];
        bool sourceAssociated = TPixelFrom.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;
        bool destinationAssociated = TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;

        // Retain associated vectors only when both formats store associated alpha, avoiding needless unassociation and lost precision.
        // Mixed formats use straight vectors so the destination applies association after quantizing its own alpha.
        bool preserveAssociatedRepresentation = sourceAssociated && destinationAssociated;

        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TPixelFrom> s = source.Slice(start, sliceLength);
            Span<TPixel> d = destination.Slice(start, sliceLength);

            if (preserveAssociatedRepresentation)
            {
                PixelOperations<TPixelFrom>.Instance.ToAssociatedScaledVector4(configuration, s, vectorSpan);
                this.FromAssociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
            else
            {
                PixelOperations<TPixelFrom>.Instance.ToUnassociatedScaledVector4(configuration, s, vectorSpan);
                this.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;
        if (remainder > 0)
        {
            ReadOnlySpan<TPixelFrom> s = source[endOfCompleteSlices..];
            Span<TPixel> d = destination[endOfCompleteSlices..];
            vectorSpan = vectorSpan[..remainder];

            if (preserveAssociatedRepresentation)
            {
                PixelOperations<TPixelFrom>.Instance.ToAssociatedScaledVector4(configuration, s, vectorSpan);
                this.FromAssociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
            else
            {
                PixelOperations<TPixelFrom>.Instance.ToUnassociatedScaledVector4(configuration, s, vectorSpan);
                this.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
        }
    }

    /// <summary>
    /// Bulk operation that copies the <paramref name="source"/> to <paramref name="destination"/> in
    /// <typeparamref name="TPixelTo"/> format.
    /// </summary>
    /// <typeparam name="TPixelTo">The destination pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TPixel}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TPixelTo}"/> to the destination pixels.</param>
    public virtual void To<TPixelTo>(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<TPixelTo> destination)
        where TPixelTo : unmanaged, IPixel<TPixelTo>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (typeof(TPixel) == typeof(TPixelTo))
        {
            // Identical formats have the same component layout and alpha representation.
            // Copy their storage directly to retain every packed component without a vector conversion.
            MemoryMarshal.Cast<TPixel, TPixelTo>(source).CopyTo(destination);
            return;
        }

        PixelOperations<TPixelTo>.Instance.From(configuration, source, destination);
    }

    /// <summary>
    /// Bulk operation that packs 3 separate RGB channels to <paramref name="destination"/>.
    /// The destination must contain at least as many pixels as each source channel.
    /// </summary>
    /// <param name="redChannel">A <see cref="ReadOnlySpan{T}"/> to the red values.</param>
    /// <param name="greenChannel">A <see cref="ReadOnlySpan{T}"/> to the green values.</param>
    /// <param name="blueChannel">A <see cref="ReadOnlySpan{T}"/> to the blue values.</param>
    /// <param name="destination">A <see cref="Span{T}"/> to the destination pixels.</param>
    internal virtual void PackFromRgbPlanes(
        ReadOnlySpan<byte> redChannel,
        ReadOnlySpan<byte> greenChannel,
        ReadOnlySpan<byte> blueChannel,
        Span<TPixel> destination)
    {
        int count = redChannel.Length;
        GuardPackFromRgbPlanes(greenChannel, blueChannel, destination, count);

        Rgb24 rgb24 = default;
        ref byte r = ref MemoryMarshal.GetReference(redChannel);
        ref byte g = ref MemoryMarshal.GetReference(greenChannel);
        ref byte b = ref MemoryMarshal.GetReference(blueChannel);
        ref TPixel d = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            rgb24.R = Unsafe.Add(ref r, i);
            rgb24.G = Unsafe.Add(ref g, i);
            rgb24.B = Unsafe.Add(ref b, i);
            Unsafe.Add(ref d, i) = TPixel.FromRgb24(rgb24);
        }
    }

    /// <summary>
    /// Bulk operation that unpacks pixels from <paramref name="source"/>
    /// into 3 separate RGB channels.
    /// </summary>
    /// <param name="redChannel">A <see cref="ReadOnlySpan{T}"/> to the red values.</param>
    /// <param name="greenChannel">A <see cref="ReadOnlySpan{T}"/> to the green values.</param>
    /// <param name="blueChannel">A <see cref="ReadOnlySpan{T}"/> to the blue values.</param>
    /// <param name="source">A <see cref="Span{T}"/> to the destination pixels.</param>
    internal virtual void UnpackIntoRgbPlanes(
        Span<float> redChannel,
        Span<float> greenChannel,
        Span<float> blueChannel,
        ReadOnlySpan<TPixel> source)
    {
        GuardUnpackIntoRgbPlanes(redChannel, greenChannel, blueChannel, source);

        // TODO: This can be much faster.
        // Convert to Rgba32 first using pixel operations then use the R, G, B properties.
        int count = source.Length;

        ref float r = ref MemoryMarshal.GetReference(redChannel);
        ref float g = ref MemoryMarshal.GetReference(greenChannel);
        ref float b = ref MemoryMarshal.GetReference(blueChannel);
        ref TPixel src = ref MemoryMarshal.GetReference(source);
        for (nuint i = 0; i < (uint)count; i++)
        {
            Rgba32 rgba32 = Unsafe.Add(ref src, i).ToRgba32();
            Unsafe.Add(ref r, i) = rgba32.R;
            Unsafe.Add(ref g, i) = rgba32.G;
            Unsafe.Add(ref b, i) = rgba32.B;
        }
    }

    /// <summary>
    /// Checks that the RGB planes have equal lengths and can hold every source pixel.
    /// </summary>
    /// <param name="redChannel">The red destination plane.</param>
    /// <param name="greenChannel">The green destination plane.</param>
    /// <param name="blueChannel">The blue destination plane.</param>
    /// <param name="source">The source pixels.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void GuardUnpackIntoRgbPlanes(
        ReadOnlySpan<float> redChannel,
        Span<float> greenChannel,
        Span<float> blueChannel,
        ReadOnlySpan<TPixel> source)
    {
        Guard.IsTrue(greenChannel.Length == redChannel.Length, nameof(greenChannel), "Channels must be of same size!");
        Guard.IsTrue(blueChannel.Length == redChannel.Length, nameof(blueChannel), "Channels must be of same size!");
        Guard.IsTrue(source.Length <= redChannel.Length, nameof(source), "'source' span should not be bigger than the destination channels!");
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void GuardPackFromRgbPlanes(ReadOnlySpan<byte> greenChannel, ReadOnlySpan<byte> blueChannel, Span<TPixel> destination, int count)
    {
        Guard.IsTrue(greenChannel.Length == count, nameof(greenChannel), "Channels must be of same size!");
        Guard.IsTrue(blueChannel.Length == count, nameof(blueChannel), "Channels must be of same size!");
        Guard.IsTrue(destination.Length >= count, nameof(destination), "'destination' span should not be shorter than the source channels!");
    }

    /// <summary>
    /// Converts associated source pixels to an unassociated destination format.
    /// </summary>
    /// <typeparam name="TPixelTo">The destination pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination pixels.</param>
    private void ConvertToUnassociated<TPixelTo>(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<TPixelTo> destination)
        where TPixelTo : unmanaged, IPixel<TPixelTo>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (source.IsEmpty)
        {
            return;
        }

        int sliceLength = Math.Min(source.Length, 1024);
        int numberOfSlices = source.Length / sliceLength;

        using IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(sliceLength);
        Span<Vector4> vectorSpan = tempVectors.GetSpan()[..sliceLength];
        PixelOperations<TPixelTo> destinationOperations = PixelOperations<TPixelTo>.Instance;

        // Generated destination conversions can route back through this source operation, so read through the source hook directly.
        // The destination then applies its own alpha rules when it packs the straight vectors.
        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TPixel> sourceSlice = source.Slice(start, sliceLength);
            Span<TPixelTo> destinationSlice = destination.Slice(start, sliceLength);
            this.ToUnassociatedScaledVector4(configuration, sourceSlice, vectorSpan);
            destinationOperations.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, destinationSlice);
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;

        if (remainder > 0)
        {
            ReadOnlySpan<TPixel> sourceSlice = source[endOfCompleteSlices..];
            Span<TPixelTo> destinationSlice = destination.Slice(endOfCompleteSlices, remainder);
            vectorSpan = vectorSpan[..remainder];
            this.ToUnassociatedScaledVector4(configuration, sourceSlice, vectorSpan);
            destinationOperations.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, destinationSlice);
        }
    }

    /// <summary>
    /// Creates and retains the bulk operations instance when <see cref="Instance"/> is first accessed.
    /// </summary>
    /// <remarks>
    /// Each closed <see cref="PixelOperations{TPixel}"/> type has its own holder and instance.
    /// The holder is separate so calls to the outer type's scalar helpers do not create bulk operations.
    /// The explicit static constructor prevents early field initialization. The runtime synchronizes that constructor,
    /// so concurrent callers share one instance without additional locks or a factory delegate.
    /// The constructor calls the existing pixel factory directly. Mono WASM AOT can compile this ordinary static abstract call.
    /// Creating a delegate directly from that interface method instead encounters the compiler defect described in
    /// <see href="https://github.com/dotnet/runtime/pull/131880">dotnet/runtime#131880</see> on affected runtimes.
    /// No list of pixel types or compile-only conversion calls is required by this initialization path.
    /// If the factory throws, the runtime leaves this holder uninitialized and reports a <see cref="TypeInitializationException"/>.
    /// It does not retry the factory on later accesses. The original exception is available through the exception's inner exception.
    /// </remarks>
    private static class InstanceHolder
    {
        /// <summary>
        /// The bulk operations instance returned by this pixel type's factory.
        /// </summary>
        public static readonly PixelOperations<TPixel> Value;

        /// <summary>
        /// Initializes static members of the <see cref="InstanceHolder"/> class.
        /// </summary>
        static InstanceHolder()
        {
            // Keep this as a direct call. A delegate from the static abstract method uses Mono's faulty delegate-construction path.
            Value = TPixel.CreatePixelOperations();
        }
    }
}

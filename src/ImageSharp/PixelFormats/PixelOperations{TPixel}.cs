// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides bulk conversion operations for pixel buffers of type <typeparamref name="TPixel"/>.
/// </summary>
/// <remarks>
/// The default conversion behavior is suitable for pixel formats that store unassociated alpha.
/// Pixel formats that store associated alpha should derive their operations from
/// <see cref="AssociatedAlphaPixelOperations{TPixel}"/>.
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public partial class PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private static readonly Lazy<PixelOperations<TPixel>> LazyInstance = new(TPixel.CreatePixelOperations, true);

    /// <summary>
    /// Gets the global <see cref="PixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>
    /// </summary>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static PixelOperations<TPixel> Instance => LazyInstance.Value;
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

        Utils.Vector4Converters.Default.UnsafeToUnassociatedVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeToAssociatedVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeToUnassociatedScaledVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeToAssociatedScaledVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeFromUnassociatedVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeFromAssociatedVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeFromUnassociatedScaledVector4<TPixel>(source, destination);
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

        Utils.Vector4Converters.Default.UnsafeFromAssociatedScaledVector4<TPixel>(source, destination);
    }

    /// <summary>
    /// Converts pixels to vectors in the native numeric range and with their native alpha representation.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToUnassociatedVector4(configuration, source, destination);

    /// <summary>
    /// Converts pixels to scaled vectors with their native alpha representation.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    public virtual void ToScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToUnassociatedScaledVector4(configuration, source, destination);

    /// <summary>
    /// Converts vectors in the native numeric range and with the pixel format's native alpha representation to pixels.
    /// The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromUnassociatedVector4Destructive(configuration, source, destination);

    /// <summary>
    /// Converts scaled vectors with the pixel format's native alpha representation to pixels. The source contents may be modified.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    public virtual void FromScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromUnassociatedScaledVector4Destructive(configuration, source, destination);

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
    /// Bulk operation that converts <paramref name="source"/> pixels from <typeparamref name="TSourcePixel"/> format to
    /// <typeparamref name="TPixel"/> destination pixels.
    /// </summary>
    /// <typeparam name="TSourcePixel">The source pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TSourcePixel}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TPixel}"/> to the destination pixels.</param>
    public virtual void From<TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
        where TSourcePixel : unmanaged, IPixel<TSourcePixel>
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
        bool sourceAssociated = TSourcePixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;
        bool destinationAssociated = TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;

        // Retain associated vectors only when both formats store associated alpha, avoiding needless unassociation and lost precision.
        // Mixed formats use straight vectors so the destination applies association after quantizing its own alpha.
        bool preserveAssociatedRepresentation = sourceAssociated && destinationAssociated;

        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TSourcePixel> s = source.Slice(start, sliceLength);
            Span<TPixel> d = destination.Slice(start, sliceLength);

            if (preserveAssociatedRepresentation)
            {
                PixelOperations<TSourcePixel>.Instance.ToAssociatedScaledVector4(configuration, s, vectorSpan);
                this.FromAssociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
            else
            {
                PixelOperations<TSourcePixel>.Instance.ToUnassociatedScaledVector4(configuration, s, vectorSpan);
                this.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;
        if (remainder > 0)
        {
            ReadOnlySpan<TSourcePixel> s = source[endOfCompleteSlices..];
            Span<TPixel> d = destination[endOfCompleteSlices..];
            vectorSpan = vectorSpan[..remainder];

            if (preserveAssociatedRepresentation)
            {
                PixelOperations<TSourcePixel>.Instance.ToAssociatedScaledVector4(configuration, s, vectorSpan);
                this.FromAssociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
            else
            {
                PixelOperations<TSourcePixel>.Instance.ToUnassociatedScaledVector4(configuration, s, vectorSpan);
                this.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, d);
            }
        }
    }

    /// <summary>
    /// Bulk operation that copies the <paramref name="source"/> to <paramref name="destination"/> in
    /// <typeparamref name="TDestinationPixel"/> format.
    /// </summary>
    /// <typeparam name="TDestinationPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TPixel}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TDestinationPixel}"/> to the destination pixels.</param>
    public virtual void To<TDestinationPixel>(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<TDestinationPixel> destination)
        where TDestinationPixel : unmanaged, IPixel<TDestinationPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (typeof(TPixel) == typeof(TDestinationPixel))
        {
            // An identical destination stores the same representation, including associated alpha, so copying preserves every packed component without a lossy representation round trip.
            MemoryMarshal.Cast<TPixel, TDestinationPixel>(source).CopyTo(destination);
            return;
        }

        PixelOperations<TDestinationPixel>.Instance.From(configuration, source, destination);
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

    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void GuardUnpackIntoRgbPlanes(ReadOnlySpan<float> redChannel, Span<float> greenChannel, Span<float> blueChannel, ReadOnlySpan<TPixel> source)
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
}

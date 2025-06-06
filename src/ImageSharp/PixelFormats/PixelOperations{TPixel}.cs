// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
/// for pixel buffers of type <typeparamref name="TPixel"/>.
/// </summary>
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
    /// Gets the pixel type info for the given <typeparamref name="TPixel"/>.
    /// </summary>
    /// <returns>The <see cref="PixelTypeInfo"/>.</returns>
    public PixelTypeInfo GetPixelTypeInfo() => TPixel.GetPixelTypeInfo();

    /// <summary>
    /// Bulk version of <see cref="IPixel{TPixel}.FromVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
    /// The method is DESTRUCTIVE altering the contents of <paramref name="sourceVectors"/>.
    /// </summary>
    /// <remarks>
    /// The destructive behavior is a design choice for performance reasons.
    /// In a typical use case the contents of <paramref name="sourceVectors"/> are abandoned after the conversion.
    /// </remarks>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
    /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
    /// <param name="destination">The <see cref="Span{T}"/> to the destination colors.</param>
    /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the conversion</param>
    public virtual void FromVector4Destructive(
        Configuration configuration,
        Span<Vector4> sourceVectors,
        Span<TPixel> destination,
        PixelConversionModifiers modifiers)
    {
        Guard.NotNull(configuration, nameof(configuration));

        Utils.Vector4Converters.Default.FromVector4(sourceVectors, destination, modifiers);
    }

    /// <summary>
    /// Bulk version of <see cref="IPixel{TPixel}.FromVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
    /// The method is DESTRUCTIVE altering the contents of <paramref name="sourceVectors"/>.
    /// </summary>
    /// <remarks>
    /// The destructive behavior is a design choice for performance reasons.
    /// In a typical use case the contents of <paramref name="sourceVectors"/> are abandoned after the conversion.
    /// </remarks>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
    /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
    /// <param name="destination">The <see cref="Span{T}"/> to the destination colors.</param>
    public void FromVector4Destructive(
        Configuration configuration,
        Span<Vector4> sourceVectors,
        Span<TPixel> destination)
        => this.FromVector4Destructive(configuration, sourceVectors, destination, PixelConversionModifiers.None);

    /// <summary>
    /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
    /// </summary>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
    /// <param name="source">The <see cref="Span{T}"/> to the source colors.</param>
    /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
    /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the conversion</param>
    public virtual void ToVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destinationVectors,
        PixelConversionModifiers modifiers)
    {
        Guard.NotNull(configuration, nameof(configuration));

        Utils.Vector4Converters.Default.ToVector4(source, destinationVectors, modifiers);
    }

    /// <summary>
    /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
    /// </summary>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
    /// <param name="source">The <see cref="Span{T}"/> to the source colors.</param>
    /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
    public void ToVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destinationVectors)
        => this.ToVector4(configuration, source, destinationVectors, PixelConversionModifiers.None);

    /// <summary>
    /// Bulk operation that copies the <paramref name="source"/> to <paramref name="destination"/> in
    /// <typeparamref name="TSourcePixel"/> format.
    /// </summary>
    /// <typeparam name="TSourcePixel">The destination pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TSourcePixel}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TPixel}"/> to the destination pixels.</param>
    public virtual void From<TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
        where TSourcePixel : unmanaged, IPixel<TSourcePixel>
    {
        const int sliceLength = 1024;
        int numberOfSlices = source.Length / sliceLength;

        using IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(sliceLength);
        Span<Vector4> vectorSpan = tempVectors.GetSpan();
        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TSourcePixel> s = source.Slice(start, sliceLength);
            Span<TPixel> d = destination.Slice(start, sliceLength);
            PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, s, vectorSpan, PixelConversionModifiers.Scale);
            this.FromVector4Destructive(configuration, vectorSpan, d, PixelConversionModifiers.Scale);
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;
        if (remainder > 0)
        {
            ReadOnlySpan<TSourcePixel> s = source[endOfCompleteSlices..];
            Span<TPixel> d = destination[endOfCompleteSlices..];
            vectorSpan = vectorSpan[..remainder];
            PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, s, vectorSpan, PixelConversionModifiers.Scale);
            this.FromVector4Destructive(configuration, vectorSpan, d, PixelConversionModifiers.Scale);
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

        PixelOperations<TDestinationPixel>.Instance.From(configuration, source, destination);
    }

    /// <summary>
    /// Bulk operation that packs 3 separate RGB channels to <paramref name="destination"/>.
    /// The destination must have a padding of 3.
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
    internal static void GuardUnpackIntoRgbPlanes(Span<float> redChannel, Span<float> greenChannel, Span<float> blueChannel, ReadOnlySpan<TPixel> source)
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
        Guard.IsTrue(destination.Length > count + 2, nameof(destination), "'destination' must contain a padding of 3 elements!");
    }
}

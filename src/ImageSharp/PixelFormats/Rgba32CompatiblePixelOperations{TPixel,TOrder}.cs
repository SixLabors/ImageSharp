// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares bulk conversion for the RGB and RGBA byte layouts that store straight alpha.
/// </summary>
/// <remarks>
/// The conversions from the luminance and 16-bit layouts load the source pixels as <see cref="Rgba32"/> pixels in
/// registers. <typeparamref name="TOrder"/> moves the channels to the order of <typeparamref name="TPixel"/>. Each
/// conversion stores the pixels in one pass. A 3-byte layout stores red, green and blue only.
/// </remarks>
/// <typeparam name="TPixel">The RGB or RGBA byte pixel format.</typeparam>
/// <typeparam name="TOrder">The shuffle from RGBA order to the channel order of <typeparamref name="TPixel"/>.</typeparam>
internal abstract class Rgba32CompatiblePixelOperations<TPixel, TOrder> : Rgba32CompatiblePixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TOrder : struct, IShuffle4
{
    /// <inheritdoc />
    public override void FromL8(Configuration configuration, ReadOnlySpan<L8> source, Span<TPixel> destination)
        => FromRgba32Source<PixelConverter.L8Rgba32Operator, L8>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromLa16(Configuration configuration, ReadOnlySpan<La16> source, Span<TPixel> destination)
        => FromRgba32Source<PixelConverter.La16Rgba32Operator, La16>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromL16(Configuration configuration, ReadOnlySpan<L16> source, Span<TPixel> destination)
        => FromRgba32Source<PixelConverter.L16Rgba32Operator, L16>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromLa32(Configuration configuration, ReadOnlySpan<La32> source, Span<TPixel> destination)
        => FromRgba32Source<PixelConverter.La32Rgba32Operator, La32>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgb48(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<TPixel> destination)
        => FromRgba32Source<PixelConverter.Rgb48Rgba32Operator, Rgb48>(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgba64(Configuration configuration, ReadOnlySpan<Rgba64> source, Span<TPixel> destination)
        => FromRgba32Source<PixelConverter.Rgba64Rgba32Operator, Rgba64>(configuration, source, destination);

    /// <summary>
    /// Converts source pixels through the <see cref="Rgba32"/> source operator of their layout.
    /// </summary>
    /// <typeparam name="TSource">The source operator.</typeparam>
    /// <typeparam name="TSourcePixel">The source pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination pixels.</param>
    private static void FromRgba32Source<TSource, TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
        where TSource : PixelConverter.IRgba32SourceOperator
        where TSourcePixel : unmanaged
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ReadOnlySpan<byte> sourceBytes = MemoryMarshal.AsBytes(source);
        Span<byte> destinationBytes = MemoryMarshal.AsBytes(destination);

        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            PixelConverter.Rgba32SourceKernels<TSource>.To4Bytes<TOrder>(sourceBytes, destinationBytes);
        }
        else
        {
            PixelConverter.Rgba32SourceKernels<TSource>.To3Bytes<TOrder>(sourceBytes, destinationBytes);
        }
    }
}

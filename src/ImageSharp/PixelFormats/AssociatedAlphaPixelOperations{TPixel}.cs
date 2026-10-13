// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides bulk operations for pixel formats that store associated alpha.
/// </summary>
/// <typeparam name="TPixel">The associated-alpha pixel format.</typeparam>
public abstract class AssociatedAlphaPixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <inheritdoc />
    public override PixelBlender<TPixel> GetPixelBlender(PixelColorBlendingMode colorMode, PixelAlphaCompositionMode alphaMode)
        => AssociatedAlphaPixelBlenders<TPixel>.GetPixelBlender(colorMode, alphaMode);

    /// <inheritdoc />
    public abstract override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination);

    /// <inheritdoc />
    public abstract override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination);

    /// <inheritdoc />
    public abstract override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination);

    /// <inheritdoc />
    public abstract override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination);

    /// <inheritdoc />
    public abstract override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination);

    /// <inheritdoc />
    public abstract override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination);

    /// <inheritdoc />
    public abstract override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination);

    /// <inheritdoc />
    public abstract override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination);

    /// <inheritdoc />
    public override void ToVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToAssociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void ToScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToAssociatedScaledVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void FromVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromAssociatedVector4Destructive(configuration, source, destination);

    /// <inheritdoc />
    public override void FromScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromAssociatedScaledVector4Destructive(configuration, source, destination);

    /// <inheritdoc />
    public override void FromArgb32(Configuration configuration, ReadOnlySpan<Argb32> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromAbgr32(Configuration configuration, ReadOnlySpan<Abgr32> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromBgr24(Configuration configuration, ReadOnlySpan<Bgr24> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromBgra32(Configuration configuration, ReadOnlySpan<Bgra32> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromL8(Configuration configuration, ReadOnlySpan<L8> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromL16(Configuration configuration, ReadOnlySpan<L16> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromLa16(Configuration configuration, ReadOnlySpan<La16> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromLa32(Configuration configuration, ReadOnlySpan<La32> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgb24(Configuration configuration, ReadOnlySpan<Rgb24> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgba32(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgb48(Configuration configuration, ReadOnlySpan<Rgb48> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromRgba64(Configuration configuration, ReadOnlySpan<Rgba64> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void FromBgra5551(Configuration configuration, ReadOnlySpan<Bgra5551> source, Span<TPixel> destination)
        => this.From(configuration, source, destination);

    /// <inheritdoc />
    public override void ToArgb32(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Argb32> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToAbgr32(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Abgr32> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToBgr24(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Bgr24> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToBgra32(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Bgra32> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToL8(Configuration configuration, ReadOnlySpan<TPixel> source, Span<L8> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToL16(Configuration configuration, ReadOnlySpan<TPixel> source, Span<L16> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToLa16(Configuration configuration, ReadOnlySpan<TPixel> source, Span<La16> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToLa32(Configuration configuration, ReadOnlySpan<TPixel> source, Span<La32> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToRgb24(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Rgb24> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToRgba32(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Rgba32> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToRgb48(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Rgb48> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToRgba64(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Rgba64> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

    /// <inheritdoc />
    public override void ToBgra5551(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Bgra5551> destination)
        => this.ConvertToUnassociated(configuration, source, destination);

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
}

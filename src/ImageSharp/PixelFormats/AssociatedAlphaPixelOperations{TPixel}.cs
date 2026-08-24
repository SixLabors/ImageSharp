// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
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
    protected abstract override void ToUnassociatedVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destination);

    /// <inheritdoc />
    protected abstract override void ToAssociatedVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destination);

    /// <inheritdoc />
    protected abstract override void FromUnassociatedVector4Destructive(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination);

    /// <inheritdoc />
    protected abstract override void FromAssociatedVector4Destructive(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination);

    /// <inheritdoc />
    protected abstract override void ToUnassociatedScaledVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destination);

    /// <inheritdoc />
    protected abstract override void ToAssociatedScaledVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destination);

    /// <inheritdoc />
    protected abstract override void FromUnassociatedScaledVector4Destructive(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination);

    /// <inheritdoc />
    protected abstract override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination);

    /// <inheritdoc />
    public override void From<TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
    {
        if (source.IsEmpty)
        {
            return;
        }

        // Cap large conversions at 1,024 vectors while avoiding a 16 KiB rental for short spans.
        int sliceLength = Math.Min(source.Length, 1024);
        int numberOfSlices = source.Length / sliceLength;

        using IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(sliceLength);
        Span<Vector4> vectorSpan = tempVectors.GetSpan()[..sliceLength];

        // Convert through unassociated vectors so the destination operation can quantize alpha to its own storage before associating RGB.
        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TSourcePixel> sourceSlice = source.Slice(start, sliceLength);
            Span<TPixel> destinationSlice = destination.Slice(start, sliceLength);
            PixelOperations<TSourcePixel>.Instance.ToVector4(
                configuration,
                sourceSlice,
                vectorSpan,
                PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            this.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, destinationSlice);
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;

        if (remainder > 0)
        {
            ReadOnlySpan<TSourcePixel> sourceSlice = source[endOfCompleteSlices..];
            Span<TPixel> destinationSlice = destination.Slice(endOfCompleteSlices, remainder);
            vectorSpan = vectorSpan[..remainder];
            PixelOperations<TSourcePixel>.Instance.ToVector4(
                configuration,
                sourceSlice,
                vectorSpan,
                PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            this.FromUnassociatedScaledVector4Destructive(configuration, vectorSpan, destinationSlice);
        }
    }

    /// <inheritdoc />
    public override void FromVector4Destructive(
        Configuration configuration,
        Span<Vector4> sourceVectors,
        Span<TPixel> destination,
        PixelConversionModifiers modifiers)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(sourceVectors, destination, nameof(destination));

        bool associated = modifiers.IsDefined(PixelConversionModifiers.Premultiply) || !modifiers.IsDefined(PixelConversionModifiers.UnPremultiply);
        bool scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);

        if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
        {
            // Transfer functions operate on straight color components. Associated input must therefore be unassociated before companding.
            if (associated)
            {
                Numerics.UnPremultiply(sourceVectors);
            }

            SRgbCompanding.Compress(sourceVectors);

            if (scaled)
            {
                this.FromUnassociatedScaledVector4Destructive(configuration, sourceVectors, destination);
            }
            else
            {
                this.FromUnassociatedVector4Destructive(configuration, sourceVectors, destination);
            }

            return;
        }

        if (scaled)
        {
            if (associated)
            {
                this.FromAssociatedScaledVector4Destructive(configuration, sourceVectors, destination);
            }
            else
            {
                this.FromUnassociatedScaledVector4Destructive(configuration, sourceVectors, destination);
            }
        }
        else if (associated)
        {
            this.FromAssociatedVector4Destructive(configuration, sourceVectors, destination);
        }
        else
        {
            this.FromUnassociatedVector4Destructive(configuration, sourceVectors, destination);
        }
    }

    /// <inheritdoc />
    public override void ToVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destinationVectors,
        PixelConversionModifiers modifiers)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destinationVectors, nameof(destinationVectors));

        bool associated = modifiers.IsDefined(PixelConversionModifiers.Premultiply) || !modifiers.IsDefined(PixelConversionModifiers.UnPremultiply);
        bool scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);

        if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
        {
            // Extract straight color before applying the transfer function; companding associated components would make RGB depend on alpha.
            if (scaled)
            {
                this.ToUnassociatedScaledVector4(configuration, source, destinationVectors);
            }
            else
            {
                this.ToUnassociatedVector4(configuration, source, destinationVectors);
            }

            Span<Vector4> converted = destinationVectors[..source.Length];
            SRgbCompanding.Expand(converted);

            if (associated)
            {
                Numerics.Premultiply(converted);
            }

            return;
        }

        if (scaled)
        {
            if (associated)
            {
                this.ToAssociatedScaledVector4(configuration, source, destinationVectors);
            }
            else
            {
                this.ToUnassociatedScaledVector4(configuration, source, destinationVectors);
            }
        }
        else if (associated)
        {
            this.ToAssociatedVector4(configuration, source, destinationVectors);
        }
        else
        {
            this.ToUnassociatedVector4(configuration, source, destinationVectors);
        }
    }

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
    /// <typeparam name="TDestinationPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination pixels.</param>
    private void ConvertToUnassociated<TDestinationPixel>(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<TDestinationPixel> destination)
        where TDestinationPixel : unmanaged, IPixel<TDestinationPixel>
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
        PixelOperations<TDestinationPixel> destinationOperations = PixelOperations<TDestinationPixel>.Instance;

        // Generated destination dispatch routes conversion back through this source operation's ToX override. Extract through this
        // operation's protected bulk hook, then use the destination's public modifier contract to avoid recursive dispatch.
        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TPixel> sourceSlice = source.Slice(start, sliceLength);
            Span<TDestinationPixel> destinationSlice = destination.Slice(start, sliceLength);
            this.ToUnassociatedScaledVector4(configuration, sourceSlice, vectorSpan);
            destinationOperations.FromVector4Destructive(configuration, vectorSpan, destinationSlice, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;

        if (remainder > 0)
        {
            ReadOnlySpan<TPixel> sourceSlice = source[endOfCompleteSlices..];
            Span<TDestinationPixel> destinationSlice = destination.Slice(endOfCompleteSlices, remainder);
            vectorSpan = vectorSpan[..remainder];
            this.ToUnassociatedScaledVector4(configuration, sourceSlice, vectorSpan);
            destinationOperations.FromVector4Destructive(configuration, vectorSpan, destinationSlice, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        }
    }
}

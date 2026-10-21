// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares bulk conversion for pixels stored as consecutive RGBA single-precision components.
/// </summary>
/// <remarks>
/// Native conversions retain storage values. Normalized color conversions apply finite bounds and alpha conversion in final storage.
/// Alpha representation comes from the pixel metadata and is selected before SIMD traversal.
/// </remarks>
/// <typeparam name="TPixel">The four-component floating-point pixel format.</typeparam>
internal abstract class RgbaVectorPixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <inheritdoc />
    public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Span<Vector4> output = destination[..source.Length];
        MemoryMarshal.Cast<TPixel, Vector4>(source).CopyTo(output);

        // Convert the copy so outward alpha conversion cannot mutate pixel storage. Native components retain IEEE values.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Numerics.UnPremultiply(output);
        }
    }

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Span<Vector4> output = destination[..source.Length];
        MemoryMarshal.Cast<TPixel, Vector4>(source).CopyTo(output);

        // Convert the copy so outward alpha conversion cannot mutate pixel storage. Native components retain IEEE values.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.Associated)
        {
            Numerics.Premultiply(output);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Write directly to final storage. Association and finite bounds share the selected SIMD traversal.
        ReadOnlySpan<Vector4> vectors = MemoryMarshal.Cast<TPixel, Vector4>(source);
        Span<Vector4> output = destination[..source.Length];

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            PixelFormats.PixelOperations.UnPremultiply(vectors, output);
        }
        else
        {
            PixelFormats.PixelOperations.ClampToPixelRange(vectors, output);
        }
    }

    /// <inheritdoc />
    public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Write directly to final storage. Association and finite bounds share the selected SIMD traversal.
        ReadOnlySpan<Vector4> vectors = MemoryMarshal.Cast<TPixel, Vector4>(source);
        Span<Vector4> output = destination[..source.Length];

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            PixelFormats.PixelOperations.ClampToPixelRange(vectors, output);
        }
        else
        {
            PixelFormats.PixelOperations.Associate(vectors, output);
        }
    }

    /// <inheritdoc />
    public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // This API permits source mutation. Apply native alpha arithmetic before copying to destination storage.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Numerics.Premultiply(source);
        }

        source.CopyTo(MemoryMarshal.Cast<TPixel, Vector4>(destination[..source.Length]));
    }

    /// <inheritdoc />
    public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // This API permits source mutation. Apply native alpha arithmetic before copying to destination storage.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.Associated)
        {
            Numerics.UnPremultiply(source);
        }

        source.CopyTo(MemoryMarshal.Cast<TPixel, Vector4>(destination[..source.Length]));
    }

    /// <inheritdoc />
    public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Write directly to final storage. Association and finite bounds share the selected SIMD traversal.
        ReadOnlySpan<Vector4> vectors = source;
        Span<Vector4> output = MemoryMarshal.Cast<TPixel, Vector4>(destination[..source.Length]);

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            PixelFormats.PixelOperations.Associate(vectors, output);
        }
        else
        {
            PixelFormats.PixelOperations.ClampToPixelRange(vectors, output);
        }
    }

    /// <inheritdoc />
    public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Write directly to final storage. Association and finite bounds share the selected SIMD traversal.
        ReadOnlySpan<Vector4> vectors = source;
        Span<Vector4> output = MemoryMarshal.Cast<TPixel, Vector4>(destination[..source.Length]);

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            PixelFormats.PixelOperations.ClampToPixelRange(vectors, output);
        }
        else
        {
            PixelFormats.PixelOperations.UnPremultiply(vectors, output);
        }
    }
}

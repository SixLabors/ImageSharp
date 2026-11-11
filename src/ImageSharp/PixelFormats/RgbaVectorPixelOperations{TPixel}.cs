// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares bulk conversion for pixels stored as consecutive RGBA single-precision components.
/// </summary>
/// <remarks>
/// Conversions that keep the alpha representation copy storage values. Conversions that change it associate or
/// unassociate in one pass that writes final storage. Alpha representation comes from the pixel metadata.
/// </remarks>
/// <typeparam name="TPixel">The four-component floating-point pixel format.</typeparam>
internal abstract class RgbaVectorPixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <inheritdoc />
    public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ReadOnlySpan<Vector4> vectors = MemoryMarshal.Cast<TPixel, Vector4>(source);
        Span<Vector4> output = destination[..source.Length];

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            PixelFormats.PixelOperations.Apply<PixelFormats.PixelOperations.UnassociateOperator>(vectors, output);
        }
        else
        {
            vectors.CopyTo(output);
        }
    }

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        ReadOnlySpan<Vector4> vectors = MemoryMarshal.Cast<TPixel, Vector4>(source);
        Span<Vector4> output = destination[..source.Length];

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            vectors.CopyTo(output);
        }
        else
        {
            PixelFormats.PixelOperations.ClampToPixelRange<PixelFormats.PixelOperations.AssociateOperator>(vectors, output);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            // The single-pixel conversion bounds the associated scaled vector first, then unassociates it.
            // The fused unpack does the same two steps in one pass, so both paths give the same result.
            this.ToAssociatedScaledVector4<PixelFormats.PixelOperations.UnassociateOperator>(configuration, source, destination);
        }
        else
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Write directly to final storage. The finite bounds use the selected SIMD traversal.
            PixelFormats.PixelOperations.ClampToPixelRange(MemoryMarshal.Cast<TPixel, Vector4>(source), destination[..source.Length]);
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
            PixelFormats.PixelOperations.ClampToPixelRange<PixelFormats.PixelOperations.AssociateOperator>(vectors, output);
        }
    }

    /// <inheritdoc />
    internal override void ToAssociatedScaledVector4<TAlpha>(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        // Associated storage is bounded to give associated scaled vectors. The alpha step runs on the bounded vectors.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelFormats.PixelOperations.ClampToPixelRange<TAlpha>(MemoryMarshal.Cast<TPixel, Vector4>(source), destination[..source.Length]);
        }
        else
        {
            base.ToAssociatedScaledVector4<TAlpha>(configuration, source, destination);
        }
    }

    /// <inheritdoc />
    public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Span<Vector4> output = MemoryMarshal.Cast<TPixel, Vector4>(destination[..source.Length]);

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            PixelFormats.PixelOperations.ClampToPixelRange<PixelFormats.PixelOperations.AssociateOperator>(source, output);
        }
        else
        {
            source.CopyTo(output);
        }
    }

    /// <inheritdoc />
    public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Span<Vector4> output = MemoryMarshal.Cast<TPixel, Vector4>(destination[..source.Length]);

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            source.CopyTo(output);
        }
        else
        {
            PixelFormats.PixelOperations.Apply<PixelFormats.PixelOperations.UnassociateOperator>(source, output);
        }
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
            PixelFormats.PixelOperations.ClampToPixelRange<PixelFormats.PixelOperations.AssociateOperator>(vectors, output);
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
            PixelFormats.PixelOperations.Apply<PixelFormats.PixelOperations.UnassociateOperator>(vectors, output);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Selects the shared storage-family implementation.
/// </content>
public partial struct RgbaVector
{
    /// <summary>
    /// Inherits the shared bulk conversions for this pixel's storage.
    /// </summary>
    internal partial class PixelOperations : RgbaVectorPixelOperations<RgbaVector>
    {
        /// <inheritdoc />
        public override void From<TPixelFrom>(Configuration configuration, ReadOnlySpan<TPixelFrom> source, Span<RgbaVector> destination)
        {
            // RgbaVector stores straight RGBA floats in Vector4 order. Write into that final storage without a temporary row or copy.
            Span<Vector4> vectors = MemoryMarshal.Cast<RgbaVector, Vector4>(destination);
            PixelOperations<TPixelFrom> sourceOperations = PixelOperations<TPixelFrom>.Instance;

            if (TPixelFrom.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
            {
                // The single-pixel associated conversions normalize stored channels before unassociation.
                // Use the same order to preserve their rounding, with both bulk operations writing only destination storage.
                sourceOperations.ToAssociatedScaledVector4(configuration, source, vectors);
                PixelFormats.PixelOperations.UnPremultiply(vectors[..source.Length], vectors[..source.Length]);
            }
            else
            {
                sourceOperations.ToUnassociatedScaledVector4(configuration, source, vectors);
            }
        }
    }
}
